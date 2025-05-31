using TheAdventure.Models;
using Silk.NET.Maths;

namespace TheAdventure.Models
{
    public class EnemyObject : RenderableGameObject
    {
        private const double Speed = 40.0; // pixels per second

        public EnemyObject(SpriteSheet spriteSheet, (int X, int Y) position)
            : base(spriteSheet, position)
        {
            SpriteSheet.ActivateAnimation("Idle"); // sau "Walk" dacă ai animație de mers
        }

        public void Update(double deltaTime, (int X, int Y) playerPos)
        {
            var dx = playerPos.X - Position.X;
            var dy = playerPos.Y - Position.Y;

            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 1e-2)
                return;

            var dirX = dx / distance;
            var dirY = dy / distance;

            var moveX = dirX * Speed * (deltaTime / 1000.0);
            var moveY = dirY * Speed * (deltaTime / 1000.0);

            Position = ((int)(Position.X + moveX), (int)(Position.Y + moveY));
        }
    }
}