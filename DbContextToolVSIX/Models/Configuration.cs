namespace DbContextToolVSIX
{
    public class Configuration
    {
        public string EntitiesNamespace { get; set; }
        public DbContextGeneratorConfiguration IDbContext { get; set; }
        public DbContextGeneratorConfiguration DbContext { get; set; }
    }
}
