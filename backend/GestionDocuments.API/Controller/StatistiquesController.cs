// backend/GestionDocuments.API/Controller/StatistiquesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Security.Claims;

[ApiController]
[Route("api/statistiques")]
[Authorize]
public class StatistiquesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public StatistiquesController(ApplicationDbContext db) => _db = db;

    // ── Dashboard Admin ─────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] int jours = 30)
    {
        var depuis = DateTime.UtcNow.AddDays(-jours);
        return Ok(new
        {
            TotalDossiers          = await _db.Dossiers.CountAsync(),
            DossiersParStatut      = await _db.Dossiers.Include(d => d.Statut)
                                        .GroupBy(d => d.Statut.Code)
                                        .Select(g => new { Statut = g.Key, Count = g.Count() })
                                        .ToListAsync(),
            TotalUtilisateurs      = await _db.Utilisateurs.CountAsync(u => !u.EstSupprime),
            UtilisateursActifs     = await _db.Utilisateurs.CountAsync(u => u.EstActif && !u.EstSupprime),
            UtilisateursListeNoire = await _db.Utilisateurs.CountAsync(u => u.EstListeNoire && !u.EstSupprime),
        });
    }

    // ── Statistiques Agent (dossiers NON archivés) ──────────
    [HttpGet("dossiers")]
    public async Task<IActionResult> GetStatsDossiers(
        [FromQuery] string  periode    = "30j",
        [FromQuery] string? dateDebut  = null,
        [FromQuery] string? dateFin    = null,
        [FromQuery] int?    serviceId  = null)
    {
        var (depuis, jusqu) = CalculerPlage(periode, dateDebut, dateFin);
        var precedent = depuis.AddDays(-(jusqu - depuis).TotalDays);

        // Exclure systématiquement les dossiers ARCHIVÉS pour l'Agent
        var query = _db.Dossiers.Include(d => d.Statut)
                                .Where(d => d.Statut.Code != "ARCHIVE")
                                .AsQueryable();
        if (serviceId.HasValue) query = query.Where(d => d.ServiceId == serviceId.Value);

        var courant   = await query.Where(d => d.DateDepot >= depuis  && d.DateDepot <= jusqu).ToListAsync();
        var precedents = await query.Where(d => d.DateDepot >= precedent && d.DateDepot < depuis).ToListAsync();

        int total    = courant.Count;
        int totalPr  = precedents.Count;
        int termines = courant.Count(d => d.Statut?.Code == "TERMINE");
        int terminPr = precedents.Count(d => d.Statut?.Code == "TERMINE");
        int rejetes  = courant.Count(d => d.Statut?.Code == "REJETE");
        int rejetPr  = precedents.Count(d => d.Statut?.Code == "REJETE");

        double delai   = courant.Where(d => d.Statut?.Code == "TERMINE")
                                .Select(d => (DateTime.UtcNow - d.DateDepot).TotalDays)
                                .DefaultIfEmpty(0).Average();
        double delaiPr = precedents.Where(d => d.Statut?.Code == "TERMINE")
                                   .Select(d => (DateTime.UtcNow - d.DateDepot).TotalDays)
                                   .DefaultIfEmpty(0).Average();

        double tTrait  = total > 0 ? Math.Round((double)termines / total * 100, 1) : 0;
        double tTraitP = totalPr > 0 ? Math.Round((double)terminPr / totalPr * 100, 1) : 0;
        double tRejet  = total > 0 ? Math.Round((double)rejetes / total * 100, 1) : 0;
        double tRejetP = totalPr > 0 ? Math.Round((double)rejetPr / totalPr * 100, 1) : 0;

        var parStatut = courant
            .GroupBy(d => new { d.Statut?.Code, d.Statut?.Libelle })
            .Select(g => new { statut = g.Key.Libelle ?? "", code = g.Key.Code ?? "", count = g.Count() })
            .ToList();

        var parService = await _db.Dossiers.Include(d => d.Service).Include(d => d.Statut)
            .Where(d => d.Statut.Code != "ARCHIVE" && d.DateDepot >= depuis && d.DateDepot <= jusqu)
            .GroupBy(d => d.Service != null ? d.Service.Nom : "Non assigné")
            .Select(g => new { service = g.Key, count = g.Count() })
            .ToListAsync();

        var delaiParMois = Enumerable.Range(0, 12)
            .Select(i => DateTime.UtcNow.AddMonths(-11 + i))
            .Select(mois => new
            {
                mois  = mois.ToString("MMM yy"),
                delai = Math.Round(
                    courant.Where(d => d.DateDepot.Month == mois.Month && d.DateDepot.Year == mois.Year)
                           .Select(d => (double)(DateTime.UtcNow - d.DateDepot).TotalDays)
                           .DefaultIfEmpty(0).Average(), 1)
            }).ToList();

        // Évolution mensuelle : SANS statut ARCHIVE
        var evolutionMensuelle = Enumerable.Range(0, 12)
            .Select(i => DateTime.UtcNow.AddMonths(-11 + i))
            .Select(mois =>
            {
                var m = courant.Where(d => d.DateDepot.Month == mois.Month && d.DateDepot.Year == mois.Year);
                return new
                {
                    mois    = mois.ToString("MMM yy"),
                    recu    = m.Count(),
                    traite  = m.Count(d => d.Statut?.Code == "TERMINE"),
                    rejete  = m.Count(d => d.Statut?.Code == "REJETE")
                };
            }).ToList();

        return Ok(new
        {
            totalDossiers           = total,
            tauxTraitement          = tTrait,
            delaiMoyen              = Math.Round(delai, 1),
            tauxRejet               = tRejet,
            tendanceTotalDossiers   = Tendance(total, totalPr),
            tendanceTauxTraitement  = Tendance(tTrait, tTraitP),
            tendanceDelaiMoyen      = Tendance(delai, delaiPr),
            tendanceTauxRejet       = Tendance(tRejet, tRejetP),
            dossiersParStatut       = parStatut,
            repartitionParService   = parService,
            delaiParMois,
            evolutionMensuelle
        });
    }

    // ── Statistiques Archiviste ─────────────────────────────
    [HttpGet("archiviste")]
    public async Task<IActionResult> GetStatsArchiviste(
        [FromQuery] string  periode   = "30j",
        [FromQuery] string? dateDebut = null,
        [FromQuery] string? dateFin   = null)
    {
        var (depuis, jusqu) = CalculerPlage(periode, dateDebut, dateFin);

        // Dossiers archivés dans la période
        var archivesPeriode = await _db.Dossiers
            .Include(d => d.Statut)
            .Where(d => d.Statut.Code == "ARCHIVE"
                     && d.DateArchivage.HasValue
                     && d.DateArchivage >= depuis
                     && d.DateArchivage <= jusqu)
            .ToListAsync();

        // Restaurations dans la période (via HistoriqueStatuts : statut -> autre que ARCHIVE)
        var restaurationsPeriode = await _db.VersionsDocument
            .Where(v => !v.EstActive == false   // versions restaurées = devenues actives à nouveau
                     && v.DateCreation >= depuis
                     && v.DateCreation <= jusqu)
            .CountAsync();

        // Évolution mensuelle sur 12 mois
        var tousArchives = await _db.Dossiers.Include(d => d.Statut)
            .Where(d => d.Statut.Code == "ARCHIVE" && d.DateArchivage.HasValue)
            .ToListAsync();

        var evolution = Enumerable.Range(0, 12)
            .Select(i => DateTime.UtcNow.AddMonths(-11 + i))
            .Select(mois => new
            {
                mois       = mois.ToString("MMM yy"),
                archives   = tousArchives.Count(d =>
                    d.DateArchivage!.Value.Month == mois.Month &&
                    d.DateArchivage.Value.Year  == mois.Year),
            }).ToList();

        // Répartition par service (dossiers archivés)
        var parService = await _db.Dossiers
            .Include(d => d.Statut).Include(d => d.Service)
            .Where(d => d.Statut.Code == "ARCHIVE")
            .GroupBy(d => d.Service != null ? d.Service.Nom : "Non assigné")
            .Select(g => new { service = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync();

        return Ok(new
        {
            totalArchivesPeriode = archivesPeriode.Count,
            totalArchivesGlobal  = tousArchives.Count,
            restaurationsPeriode,
            evolution,
            parService
        });
    }

    // ── Export PDF (CORRIGÉ — iTextSharp) ──────────────────
   // ── Export PDF (CORRIGÉ — iTextSharp.LGPLv2.Core) ──────────────────
[HttpGet("export/pdf")]
public async Task<IActionResult> ExporterPdf(
    [FromQuery] string periode   = "30j",
    [FromQuery] int?   serviceId = null)
{
    var (depuis, jusqu) = CalculerPlage(periode);

    var query = _db.Dossiers.Include(d => d.Statut)
        .Where(d => d.Statut.Code != "ARCHIVE"
                 && d.DateDepot >= depuis
                 && d.DateDepot <= jusqu)
        .AsQueryable();
    if (serviceId.HasValue) query = query.Where(d => d.ServiceId == serviceId.Value);

    var dossiers = await query.ToListAsync();
    int total    = dossiers.Count;
    int termines = dossiers.Count(d => d.Statut?.Code == "TERMINE");
    int rejetes  = dossiers.Count(d => d.Statut?.Code == "REJETE");
    double tTrait = total > 0 ? Math.Round((double)termines / total * 100, 1) : 0;
    double tRejet = total > 0 ? Math.Round((double)rejetes  / total * 100, 1) : 0;

    using var ms  = new System.IO.MemoryStream();
    var doc       = new Document(PageSize.A4, 40, 40, 60, 40);
    PdfWriter.GetInstance(doc, ms);
    doc.Open();

    var fTitre  = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, new BaseColor(21,101,192));
    var fNormal = FontFactory.GetFont(FontFactory.HELVETICA, 11);
    var fBold   = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11);
    var fSub    = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(96,125,139));

    doc.Add(new Paragraph("Rapport de Statistiques Documentaires", fTitre));
    doc.Add(new Paragraph($"Période : {depuis:dd/MM/yyyy} — {jusqu:dd/MM/yyyy}", fSub));
    doc.Add(new Paragraph($"Généré le : {DateTime.Now:dd/MM/yyyy à HH:mm}", fSub));
    doc.Add(new Paragraph(" "));   // ← remplace Chunk.NEWLINE

    // KPIs
    var kpiTable = new PdfPTable(4) { WidthPercentage = 100 };
    kpiTable.SetWidths(new float[] { 1,1,1,1 });

    void AddKpi(string label, string val, string hex)
    {
        var r = Convert.ToInt32(hex[1..3], 16);
        var g = Convert.ToInt32(hex[3..5], 16);
        var b = Convert.ToInt32(hex[5..7], 16);
        var bg = new BaseColor(r,g,b);
        var c  = new PdfPCell { BackgroundColor = bg, Padding = 10, Border = 0 };
        c.AddElement(new Paragraph(val, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 22, BaseColor.White)));   // White
        c.AddElement(new Paragraph(label, FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.White)));        // White
        kpiTable.AddCell(c);
    }

    AddKpi("Total dossiers",     total.ToString(),   "#1565C0");
    AddKpi("Traités",            termines.ToString(), "#43A047");
    AddKpi("Taux de traitement", $"{tTrait}%",       "#5E35B1");
    AddKpi("Taux de rejet",      $"{tRejet}%",       "#E53935");
    doc.Add(kpiTable);
    doc.Add(new Paragraph(" "));   // ← remplace Chunk.NEWLINE

    // Tableau par statut
    doc.Add(new Paragraph("Répartition par statut", fBold));
    doc.Add(new Paragraph(" "));   // ← remplace Chunk.NEWLINE

    var parStatut = dossiers.GroupBy(d => d.Statut?.Libelle ?? "Inconnu")
        .Select(g => (Statut: g.Key, Count: g.Count())).ToList();

    var tbl = new PdfPTable(2) { WidthPercentage = 55 };
    tbl.SetWidths(new float[] { 3, 1 });

    var hBg = new BaseColor(38,50,56);
    var hCss = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.White);
    tbl.AddCell(new PdfPCell(new Phrase("Statut",  hCss)) { BackgroundColor = hBg, Padding = 7 });
    tbl.AddCell(new PdfPCell(new Phrase("Nombre",  hCss)) { BackgroundColor = hBg, Padding = 7 });

    bool pair = false;
    foreach (var (statut, count) in parStatut)
    {
        var rowBg = pair ? new BaseColor(245,247,250) : BaseColor.White;
        tbl.AddCell(new PdfPCell(new Phrase(statut, fNormal)) { BackgroundColor = rowBg, Padding = 6 });
        tbl.AddCell(new PdfPCell(new Phrase(count.ToString(), fNormal)) { BackgroundColor = rowBg, Padding = 6 });
        pair = !pair;
    }
    doc.Add(tbl);
    doc.Close();

    return File(ms.ToArray(), "application/pdf",
        $"rapport_statistiques_{DateTime.Now:yyyyMMdd}.pdf");
}

    // ── Export Excel ────────────────────────────────────────
    [HttpGet("export/excel")]
    public async Task<IActionResult> ExporterExcel(
        [FromQuery] string periode   = "30j",
        [FromQuery] int?   serviceId = null)
    {
        var (depuis, jusqu) = CalculerPlage(periode);
        var query = _db.Dossiers.Include(d => d.Statut).Include(d => d.Service)
            .Where(d => d.Statut.Code != "ARCHIVE"
                     && d.DateDepot >= depuis
                     && d.DateDepot <= jusqu)
            .AsQueryable();
        if (serviceId.HasValue) query = query.Where(d => d.ServiceId == serviceId.Value);
        var dossiers = await query.ToListAsync();

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Statistiques");
        ws.Cell(1,1).Value = "Numéro";    ws.Cell(1,2).Value = "Statut";
        ws.Cell(1,3).Value = "Service";   ws.Cell(1,4).Value = "Date dépôt";
        for (int i = 0; i < dossiers.Count; i++)
        {
            var d = dossiers[i];
            ws.Cell(i+2,1).Value = d.Numero;
            ws.Cell(i+2,2).Value = d.Statut?.Libelle ?? "";
            ws.Cell(i+2,3).Value = d.Service?.Nom ?? "";
            ws.Cell(i+2,4).Value = d.DateDepot.ToString("dd/MM/yyyy");
        }
        ws.Columns().AdjustToContents();
        using var ms = new System.IO.MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"statistiques_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    // ── Helpers ─────────────────────────────────────────────
    private static (DateTime depuis, DateTime jusqu) CalculerPlage(
        string periode, string? dateDebut = null, string? dateFin = null)
    {
        var fin = DateTime.UtcNow;
        var deb = periode switch
        {
            "7j"     => fin.AddDays(-7),
            "90j"    => fin.AddDays(-90),
            "custom" when dateDebut != null => DateTime.Parse(dateDebut),
            _        => fin.AddDays(-30)
        };
        if (periode == "custom" && dateFin != null) fin = DateTime.Parse(dateFin);
        return (deb, fin);
    }

    private static double Tendance(double actuel, double prec) =>
        prec == 0 ? 0 : Math.Round((actuel - prec) / prec * 100, 1);
}