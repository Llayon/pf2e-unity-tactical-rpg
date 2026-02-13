namespace PF2e.Core
{
    public enum CreatureSize
    {
        Tiny,
        Small,
        Medium,
        Large,
        Huge,
        Gargantuan
    }

    public static class CreatureSizeExtensions
    {
        /// <summary>
        /// How many grid cells per side this creature occupies.
        /// Tiny returns 1 (special sharing rules not implemented yet).
        /// </summary>
        public static int ToSizeCells(this CreatureSize size)
        {
            switch (size)
            {
                case CreatureSize.Tiny:        return 1;
                case CreatureSize.Small:       return 1;
                case CreatureSize.Medium:      return 1;
                case CreatureSize.Large:       return 2;
                case CreatureSize.Huge:        return 3;
                case CreatureSize.Gargantuan:  return 4;
                default:                       return 1;
            }
        }
    }
}
