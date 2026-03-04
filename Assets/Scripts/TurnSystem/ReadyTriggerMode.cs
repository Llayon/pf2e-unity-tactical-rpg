namespace PF2e.TurnSystem
{
    /// <summary>
    /// Configurable trigger mode for prepared Ready Strike.
    /// </summary>
    public enum ReadyTriggerMode
    {
        Movement = 0,
        Attack = 1,
        Any = 2
    }

    public static class ReadyTriggerModeExtensions
    {
        public static bool AllowsMovement(this ReadyTriggerMode mode)
        {
            return mode == ReadyTriggerMode.Movement || mode == ReadyTriggerMode.Any;
        }

        public static bool AllowsAttack(this ReadyTriggerMode mode)
        {
            return mode == ReadyTriggerMode.Attack || mode == ReadyTriggerMode.Any;
        }

        public static string ToShortToken(this ReadyTriggerMode mode)
        {
            return mode switch
            {
                ReadyTriggerMode.Movement => "Move",
                ReadyTriggerMode.Attack => "Attack",
                _ => "Any"
            };
        }

        public static string ToPrepareDescription(this ReadyTriggerMode mode)
        {
            return mode switch
            {
                ReadyTriggerMode.Movement => "enemy movement",
                ReadyTriggerMode.Attack => "enemy attack",
                _ => "enemy movement or attack"
            };
        }
    }
}
