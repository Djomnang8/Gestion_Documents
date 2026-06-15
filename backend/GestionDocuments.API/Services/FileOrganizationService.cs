using System.Text.RegularExpressions;

namespace GestionDocuments.API.Services
{
    public class FileOrganizationService : IFileOrganizationService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileOrganizationService> _logger;

        public FileOrganizationService(IWebHostEnvironment env, ILogger<FileOrganizationService> logger)
        {
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Génère un nom de dossier lisible basé sur le nom et l'email du citoyen.
        /// Remplace les caractères interdits dans les noms de dossier par des underscores.
        /// </summary>
        private static string BuildCitizenFolderName(string nomCitoyen, string emailCitoyen)
        {
            var raw = $"{nomCitoyen}_{emailCitoyen}";
            // Remplace tout caractère non alphanumérique, underscore, tiret ou point par underscore
            // sauf l'arobase et le point qu'on conserve pour la lisibilité.
            var safe = Regex.Replace(raw, @"[^a-zA-Z0-9_@.-]", "_");
            // Limite la longueur (NTFS max 255, on prend 200)
            if (safe.Length > 200)
                safe = safe.Substring(0, 200);
            return safe;
        }

        public string GetCitizenFolderPath(string nomCitoyen, string emailCitoyen, int serviceId, string nomService)
        {
            var serviceFolder = SanitizeFolderName(nomService);
            var citizenFolder = BuildCitizenFolderName(nomCitoyen, emailCitoyen);
            var path = Path.Combine(_env.ContentRootPath, "uploads", "Services", serviceFolder, citizenFolder);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string SanitizeFolderName(string name)
        {
            var safe = Regex.Replace(name, @"[^a-zA-Z0-9_ -]", "");
            return string.IsNullOrWhiteSpace(safe) ? "Service_" + DateTime.Now.Ticks : safe;
        }

        public async Task<string> OrganizeFileAsync(string nomCitoyen, string emailCitoyen, int serviceId, string nomService, string sourceFilePath)
        {
            var targetFolder = GetCitizenFolderPath(nomCitoyen, emailCitoyen, serviceId, nomService);
            var fileName = Path.GetFileName(sourceFilePath);
            var targetPath = Path.Combine(targetFolder, fileName);

            if (File.Exists(sourceFilePath))
            {
                File.Move(sourceFilePath, targetPath);
            }
            else
            {
                _logger.LogWarning("Fichier source introuvable pour l'organisation : {Path}", sourceFilePath);
            }
            return targetPath;
        }

        public Task DeleteCitizenFolderAsync(string nomCitoyen, string emailCitoyen, int serviceId, string nomService)
        {
            var folder = GetCitizenFolderPath(nomCitoyen, emailCitoyen, serviceId, nomService);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
            return Task.CompletedTask;
        }

        public Task<List<string>> GetCitizenFilesAsync(string nomCitoyen, string emailCitoyen, int serviceId, string nomService)
        {
            var folder = GetCitizenFolderPath(nomCitoyen, emailCitoyen, serviceId, nomService);
            if (!Directory.Exists(folder))
                return Task.FromResult(new List<string>());
            return Task.FromResult(Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToList());
        }
    }
}