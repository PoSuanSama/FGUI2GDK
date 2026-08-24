#!/usr/bin/env bash

set -u

log_dir="${GDK_LOG_DIR:-/gdk/Logs}"
max_size_mb="${GDK_LOG_MAX_SIZE_MB:-5120}"
target_size_mb="${GDK_LOG_TARGET_SIZE_MB:-4096}"
check_interval_seconds="${GDK_LOG_CHECK_INTERVAL_SECONDS:-60}"

require_positive_integer() {
    local name="$1"
    local value="$2"

    if [[ ! "$value" =~ ^[1-9][0-9]*$ ]]; then
        printf 'log-retention: %s must be a positive integer, got %q\n' "$name" "$value" >&2
        exit 1
    fi
}

require_positive_integer GDK_LOG_MAX_SIZE_MB "$max_size_mb"
require_positive_integer GDK_LOG_TARGET_SIZE_MB "$target_size_mb"
require_positive_integer GDK_LOG_CHECK_INTERVAL_SECONDS "$check_interval_seconds"

if (( target_size_mb >= max_size_mb )); then
    printf 'log-retention: target size (%s MB) must be smaller than max size (%s MB)\n' \
        "$target_size_mb" "$max_size_mb" >&2
    exit 1
fi

max_size_bytes=$((max_size_mb * 1024 * 1024))
target_size_bytes=$((target_size_mb * 1024 * 1024))

mkdir -p -- "$log_dir"

directory_size_bytes() {
    local total=0
    local size

    while IFS= read -r -d '' size; do
        total=$((total + size))
    done < <(find "$log_dir" -maxdepth 1 -type f -printf '%s\0')

    printf '%s' "$total"
}

prune_logs() {
    local total
    local modified
    local size
    local file
    local deleted_files=0
    local deleted_bytes=0

    total="$(directory_size_bytes)"
    if (( total <= max_size_bytes )); then
        return
    fi

    while IFS=$'\t' read -r -d '' modified size file; do
        if (( total <= target_size_bytes )); then
            break
        fi

        if [[ ! -f "$file" ]]; then
            continue
        fi

        if rm -f -- "$file"; then
            total=$((total - size))
            deleted_files=$((deleted_files + 1))
            deleted_bytes=$((deleted_bytes + size))
        fi
    done < <(find "$log_dir" -maxdepth 1 -type f -printf '%T@\t%s\t%p\0' | sort -z -n)

    printf 'log-retention: deleted %s oldest files (%s MB); remaining files use %s MB\n' \
        "$deleted_files" "$((deleted_bytes / 1024 / 1024))" "$((total / 1024 / 1024))"
}

printf 'log-retention: monitoring %s (max %s MB, prune target %s MB, interval %ss)\n' \
    "$log_dir" "$max_size_mb" "$target_size_mb" "$check_interval_seconds"

prune_logs
while sleep "$check_interval_seconds"; do
    prune_logs
done
