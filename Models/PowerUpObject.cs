using Silk.NET.SDL;

namespace TheAdventure.Models
{
    public abstract class PowerUpObject : RenderableGameObject
    {
        protected PowerUpObject(SpriteSheet spriteSheet, (int X, int Y) position)
            : base(spriteSheet, position)
        {
        }

        public abstract void Apply(PlayerObject player);
    }
}