using System;

namespace ET.Server
{
    internal static class AgentProcessManagementOptions
    {
        private const string ManageProcessesEnvironmentVariable = "GDK_AGENT_MANAGE_PROCESSES";

        public static bool Enabled
        {
            get
            {
                string value = Environment.GetEnvironmentVariable(ManageProcessesEnvironmentVariable);
                return !bool.TryParse(value, out bool enabled) || enabled;
            }
        }
    }
}
