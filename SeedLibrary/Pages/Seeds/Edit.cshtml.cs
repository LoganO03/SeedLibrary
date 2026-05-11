using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SeedLibrary.Data;
using SeedLibrary.Models;

namespace SeedLibrary.Pages.Seeds
{
    public class EditModel : PageModel
    {
        private readonly SeedContext _context;

        public EditModel(SeedContext context)
        {
            _context = context;
        }

        [BindProperty]
        public SeedPacket SeedPacket { get; set; } = default!;

        [BindProperty]
        public Donation Donation { get; set; } = default!;

        [BindProperty]
        public List<int> SelectedPlantingDateIds { get; set; } = new();

        public List<SelectListItem> PlantingDateOptions { get; set; } = new();
        public string CurrentCommonName { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            SeedPacket = await _context.SeedPackets
                .Include(s => s.Variety)
                .Include(s => s.Donations)
                .Include(s => s.Growings)
                .FirstOrDefaultAsync(m => m.SeedId == id);

            if (SeedPacket == null) return NotFound();

            SelectedPlantingDateIds = SeedPacket.Growings
                .Select(g => g.PlantingDatesId)
                .ToList();

            Donation = SeedPacket.Donations.FirstOrDefault() ?? new Donation();
            CurrentCommonName = SeedPacket.Variety?.CommonNameName ?? string.Empty;

            PopulateDropdowns(Donation.SourceId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns(Donation.SourceId);
                return Page();
            }

            _context.Update(SeedPacket);

            var existingDonation = await _context.Donations
                .FirstOrDefaultAsync(d => d.SeedId == SeedPacket.SeedId);

            if (existingDonation != null)
            {
                existingDonation.SourceId = Donation.SourceId;
                existingDonation.Year = Donation.Year;
            }
            else
            {
                _context.Donations.Add(new Donation
                {
                    SeedId = SeedPacket.SeedId,
                    SourceId = Donation.SourceId,
                    Year = Donation.Year
                });
            }

            var existingGrowings = _context.Growings.Where(g => g.SeedId == SeedPacket.SeedId);
            _context.Growings.RemoveRange(existingGrowings);
            _context.Growings.AddRange(SelectedPlantingDateIds.Select(id => new Growing
            {
                SeedId = SeedPacket.SeedId,
                PlantingDatesId = id
            }));

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SeedPacketExists(SeedPacket.SeedId))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToPage("./Index");
        }

        private bool SeedPacketExists(int id) =>
            _context.SeedPackets.Any(e => e.SeedId == id);

        private void PopulateDropdowns(int? selectedSourceId = null)
        {
            ViewData["CommonName"] = new SelectList(_context.CommonNames, "Name", "Name");
            ViewData["SourceId"] = new SelectList(_context.Sources, "Id", "SourceName", selectedSourceId);
            ViewData["VarietiesJson"] = System.Text.Json.JsonSerializer.Serialize(
                _context.Varieties.Select(v => new { v.VarietyName, v.CommonNameName }).ToList()
            );

            PlantingDateOptions = _context.PlantingDates.Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"{p.StartMonth} - {System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(p.StartMonth)}" +
                       $" to {p.EndMonth} - {System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(p.EndMonth)}"
            }).ToList();
        }
    }
}