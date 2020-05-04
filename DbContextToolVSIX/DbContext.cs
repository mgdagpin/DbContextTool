using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DbContextToolVSIX
{
    public class DbContext
    {
        private Configuration JsonConfig { get; set; }
        public string SolutionPath { get; set; }
        public string JsonConfigurationPath { get; set; }


        public DbContext(string solutionPath)
        {
            var _jsonFound = Directory.GetFiles(solutionPath, "_config.json", SearchOption.AllDirectories)
                .SingleOrDefault();

            if (string.IsNullOrEmpty(_jsonFound)) throw new FileNotFoundException("_config.json is not found on this solution.");
            this.SolutionPath = solutionPath;


            var _entityPath = Directory.GetDirectories(solutionPath, "Entities", SearchOption.AllDirectories)
                .SingleOrDefault();

            if (string.IsNullOrEmpty(_entityPath)) throw new DirectoryNotFoundException("Entities directory not found on this solution");

            this.JsonConfigurationPath = _entityPath;

            string _configJson = File.ReadAllText(_jsonFound);

            Configuration _config = JsonSerializer.Deserialize<Configuration>(_configJson);
            _config.IDbContext.Path = RevealFullPath(_config.IDbContext.Path);
            _config.DbContext.Path = RevealFullPath(_config.DbContext.Path);

            this.JsonConfig = _config;
        }

        private string RevealFullPath(string segment)
        {
            var _dirs = Directory.GetDirectories(this.SolutionPath, "*", SearchOption.AllDirectories)
                .ToList();

            var _cleanSegment = segment.Replace("../", "").Replace("/", @"\").Replace(@"\\", @"\");

            var _f = _dirs
                .Where(a => a.Contains(_cleanSegment))
                .FirstOrDefault();

           if (string.IsNullOrEmpty(_f)) throw new DirectoryNotFoundException($"Cannot find {_cleanSegment}.");

           return _f;
        }

        public Configuration GetJsonConfiguration()
        {
            return this.JsonConfig;
        }

        private string[] GetFileNames(string path, string filter)
        {
            string[] _files = Directory.GetFiles(path, filter);
            for (int i = 0; i < _files.Length; i++)
                _files[i] = Path.GetFileNameWithoutExtension(_files[i]);
            return _files;
        }

        public void Generate()
        {



            string _interface = $@"
using {this.JsonConfig.EntitiesNamespace};
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace {this.JsonConfig.IDbContext.Namespace}
{{
    public interface {this.JsonConfig.IDbContext.Name}
    {{
        {this.GetProperties(GenerateType.Interface)}

        DatabaseFacade Database {{ get; }}
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        
    }}
}}
";

            string _class = $@"
using {this.JsonConfig.EntitiesNamespace};
using {this.JsonConfig.IDbContext.Namespace};
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace {this.JsonConfig.DbContext.Namespace}
{{
    public class {this.JsonConfig.DbContext.Name} : DbContext, {this.JsonConfig.IDbContext.Name}
    {{
        {this.GetProperties(GenerateType.Class)}
        
        public {this.JsonConfig.DbContext.Name}(DbContextOptions<{this.JsonConfig.DbContext.Name}> dbContextOpt) : base(dbContextOpt)
        {{

        }}  
    }}

}}
";

            this.MakeFile($@"{this.JsonConfig.IDbContext.Path}\{this.JsonConfig.IDbContext.Name}.cs", _interface);
            this.MakeFile($@"{this.JsonConfig.DbContext.Path}\{this.JsonConfig.DbContext.Name}.cs", _class);
        }

        private string GetProperties(GenerateType type)
        {
            string[] _childs = this.GetFileNames(this.JsonConfigurationPath, "*.cs")
                .OrderBy(or => or)
                .ToArray();

            StringBuilder _dbSets = new StringBuilder();
            foreach (var child in _childs.Select((Value, i) => new { Value, i }))
            {
                _dbSets.AppendFormat("{0}{1}DbSet<{2}> {3} {{get;set;}}\n",
                    (child.i > 0) ? "\t\t" : string.Empty,
                    (type == GenerateType.Class) ? "public " : string.Empty,
                    child.Value,
                    (child.Value.EndsWith("Y")) ? child.Value.Remove(child.Value.Length - 1, 1) + "ies" : child.Value + "s");
            }

            return _dbSets.ToString();
        }

        private void MakeFile(string pathString, string content)
        {
            using (StreamWriter _sw = new StreamWriter(pathString))
            {
                _sw.WriteLine(content);
            }
        }
    }
}
