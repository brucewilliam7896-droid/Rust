namespace RustPlus.Core.Save
{
    /// <summary>The one definition of a brand-new game's starting state.</summary>
    public static class SaveDefaults
    {
        public const string DefaultPlayerName = "Survivor";
        public const int MaxVital = 100;

        public static SaveGameData CreateNewGame(ulong worldSeed = 0UL)
        {
            return new SaveGameData
            {
                SchemaVersion = SaveGameData.CurrentSchemaVersion,
                Meta = new SaveMetadata(),
                Player = new PlayerSaveData
                {
                    Name = DefaultPlayerName,
                    Health = MaxVital,
                    Hunger = MaxVital,
                    Thirst = MaxVital,
                    PositionX = 0f,
                    PositionY = 0.5f,
                    PositionZ = 0f,
                    Inventory = new[] { "Stone", "Wood" }
                },
                World = new WorldSaveData
                {
                    Seed = worldSeed,
                    LastChunkX = 0,
                    LastChunkZ = 0
                }
            };
        }
    }
}
