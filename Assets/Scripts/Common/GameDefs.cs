// File: Common/GameDefs.cs
namespace GameJam.Common
{
    public enum ElementType
    {
        Fire = 0,
        Water = 1,
        Nature = 2
    }

    public static class GameDefs
    {
        public const int ElementCount = 3;

        public static string ElementToText(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire: return "Fire";
                case ElementType.Water: return "Water";
                case ElementType.Nature: return "Nature";
                default: return e.ToString();
            }
        }
    }
}
