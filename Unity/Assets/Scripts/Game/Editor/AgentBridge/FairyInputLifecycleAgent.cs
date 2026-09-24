using System;
using AgentBridge;
using Cysharp.Threading.Tasks;
using UnityEditor;

namespace Game.Editor
{
    public static class FairyInputLifecycleAgent
    {
        [AgentCallable(
            "Immediately restart FairyInputService in the same frame and verify one poll per frame plus no polling after shutdown.",
            30)]
        public static async UniTask ValidateImmediateShutdownReinitialize()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "FairyInputService lifecycle validation requires PlayMode.");
            }

            FairyInputService input = FairyInputService.Instance;
            bool restoreInitialized = input.IsInitializedForDiagnostics;
            try
            {
                input.Initialize();
                long previousGeneration = input.ActiveRegistrationGenerationForDiagnostics;

                input.Shutdown();
                input.Initialize();

                long restartedGeneration = input.ActiveRegistrationGenerationForDiagnostics;
                if (!input.IsInitializedForDiagnostics ||
                    restartedGeneration != previousGeneration + 1)
                {
                    throw new InvalidOperationException(
                        "FairyInputService did not replace the stopped PlayerLoop registration. " +
                        $"Previous generation={previousGeneration}, " +
                        $"active generation={restartedGeneration}, " +
                        $"initialized={input.IsInitializedForDiagnostics}.");
                }

                input.Initialize();
                long idempotentGeneration = input.ActiveRegistrationGenerationForDiagnostics;
                if (idempotentGeneration != restartedGeneration)
                {
                    throw new InvalidOperationException(
                        "Repeated FairyInputService.Initialize created another active registration. " +
                        $"Expected generation={restartedGeneration}, " +
                        $"actual={idempotentGeneration}.");
                }

                // Drain the retired registration once, then measure three complete frames.
                // The pre-fix service registered itself twice and would poll twice on every frame.
                await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
                input.ResetDiagnostics();
                for (int frame = 1; frame <= 3; frame++)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
                    int pollCount = input.PollCountForDiagnostics;
                    if (pollCount != frame)
                    {
                        throw new InvalidOperationException(
                            "FairyInputService PlayerLoop polling was not exactly once per frame. " +
                            $"Measured frames={frame}, polls={pollCount}.");
                    }
                }

                input.Shutdown();
                if (input.IsInitializedForDiagnostics ||
                    input.ActiveRegistrationGenerationForDiagnostics != 0)
                {
                    throw new InvalidOperationException(
                        "FairyInputService retained an active registration after shutdown. " +
                        $"Generation={input.ActiveRegistrationGenerationForDiagnostics}, " +
                        $"initialized={input.IsInitializedForDiagnostics}.");
                }

                int stoppedPollCount = input.PollCountForDiagnostics;
                for (int frame = 0; frame < 3; frame++)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
                }

                int afterShutdownPollCount = input.PollCountForDiagnostics;
                if (afterShutdownPollCount != stoppedPollCount)
                {
                    throw new InvalidOperationException(
                        "FairyInputService continued polling after shutdown. " +
                        $"Polls before wait={stoppedPollCount}, " +
                        $"polls after wait={afterShutdownPollCount}.");
                }
            }
            finally
            {
                input.Shutdown();
                if (restoreInitialized)
                {
                    input.Initialize();
                }
            }
        }
    }
}
