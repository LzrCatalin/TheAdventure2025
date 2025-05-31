namespace TheAdventure.Models;

public class CoinPowerUp : PowerUpObject
{
    public CoinPowerUp(SpriteSheet spriteSheet, (int X, int Y) position)
        : base(spriteSheet, position)
    {
        spriteSheet.ActiveAnimation = null;
    }

    public override void Apply(PlayerObject player)
    {
        player.AddScore(10);
    }
}