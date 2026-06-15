using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GestionDocuments.API.Services
{
    public interface IFileOrganizationService
    {
        /// <summary>Construit le chemin du dossier citoyen : uploads/Services/{nomService}/{dossierCitoyen}/</summary>
        string GetCitizenFolderPath(string nomCitoyen, string emailCitoyen, int serviceId, string nomService);
        /// <summary>Déplace un fichier vers le dossier organisé et retourne le nouveau chemin</summary>
        Task<string> OrganizeFileAsync(string nomCitoyen, string emailCitoyen, int serviceId, string nomService, string sourceFilePath);
        Task DeleteCitizenFolderAsync(string nomCitoyen, string emailCitoyen, int serviceId, string nomService);
        Task<List<string>> GetCitizenFilesAsync(string nomCitoyen, string emailCitoyen, int serviceId, string nomService);
    }
}