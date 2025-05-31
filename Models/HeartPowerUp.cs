namespace TheAdventure.Models;

public class HeartPowerUp : PowerUpObject
{
    public HeartPowerUp(SpriteSheet spriteSheet, (int X, int Y) position)
        : base(spriteSheet, position)
    {
        spriteSheet.ActiveAnimation = null;
    }

    public override void Apply(PlayerObject player)
    {
        if (player.Lives < 5)
        {
            player.SetLives(player.Lives + 1);
        }
    }
}