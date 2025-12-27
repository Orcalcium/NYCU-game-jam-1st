// File: Common/GameDefs.cs
using UnityEngine;

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

        public static Color ElementToColor(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire: return new Color(1f, 0.35f, 0.25f, 1f);
                case ElementType.Water: return new Color(0.25f, 0.55f, 1f, 1f);
                case ElementType.Nature: return new Color(0.35f, 0.9f, 0.45f, 1f);
                default: return Color.white;
            }
        }
    }

    public interface IElementDamageable
    {
        bool CanBeHitBy(ElementType element, Object source);
        void TakeElementHit(ElementType element, int damage, Object source);
    }
}
