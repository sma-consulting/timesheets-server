using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace sma.plan
{
	// One row of SMA's Access invoice table (tbInvoice): what an invoice
	// really came to, and whether and when it was paid.
	internal class AccessInvoice
	{
		public int Number { get; set; }
		// The job's SMA code, e.g. "505".
		public string Job { get; set; }
		public decimal Labour { get; set; }
		public decimal Disbursement { get; set; }
		public decimal Adjustment { get; set; }
		public decimal Tax { get; set; }
		// The last day of the month billed.
		public DateOnly? InvoiceDate { get; set; }
		public bool Paid { get; set; }
		public DateOnly? PaymentDate { get; set; }
		public string AdjustmentNote { get; set; }

		public decimal Total => Math.Round(Labour + Disbursement + Adjustment + Tax, 2);
	}

	// One row of Access's client table (tbClient).
	internal class AccessClient
	{
		// "clt003".
		public string Id { get; set; }
		public string Name { get; set; }
		public bool Inactive { get; set; }
		// "AECOM".
		public string Abbreviation { get; set; }
	}

	// One row of Access's project table (tbProject): a job, its client and
	// project manager by name, and its budget. The disbursement rate is a
	// fraction (0.03 for 3%).
	internal class AccessProject
	{
		public string Status { get; set; }
		public string Job { get; set; }
		public string Name { get; set; }
		public string Client { get; set; }
		public string Division { get; set; }
		public string Po { get; set; }
		public DateOnly? Start { get; set; }
		public DateOnly? End { get; set; }
		public string ProjectManager { get; set; }
		public decimal? DisbursementRate { get; set; }
		public decimal ContractValue { get; set; }
		public decimal ChangeOrder { get; set; }
		public decimal MajorDisbursement { get; set; }
		public decimal ProjectDisbursement { get; set; }
		public decimal Subconsultant { get; set; }
		public decimal TotalBudget { get; set; }
		public string Notes { get; set; }
	}

	// SMA's Access tables as exported to Excel into one folder - the newest
	// tbInvoice, tbClient and tbProject file there (tbInvoice_20261028.xlsx),
	// each its first sheet: a header row of the table's columns, then a row
	// each. Read with nothing but the standard library. A table with no file
	// is just empty.
	//
	// The folder may also hold rate-list-groups.txt: named groups of jobs to
	// share a rate list, one a line - "VL rates: 405, 493, 494" (the name
	// optional). The import keeps a job in its group only if its rates agree.
	internal class AccessExport
	{
		private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
		private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

		public const string RateListGroupsFile = "rate-list-groups.txt";

		public Dictionary<int, AccessInvoice> Invoices { get; } = new();
		public List<AccessClient> Clients { get; } = new();
		public List<AccessProject> Projects { get; } = new();
		// The jobs sharing each rate list: its name (null when the line gives
		// none) and the jobs' codes.
		public List<(string Name, List<string> Jobs)> RateListGroups { get; } = new();
		// The files read, by name; none when the folder has none of them.
		public List<string> Files { get; } = new();

		public static AccessExport Load(string folder)
		{
			var export = new AccessExport();
			string invoices = Newest(folder, "tbInvoice", "tblInvoice");
			string clients = Newest(folder, "tbClient", "tblClient");
			string projects = Newest(folder, "tbProject", "tblProject");
			export.Files.AddRange(new[] { invoices, clients, projects }.Where(f => f != null).Select(Path.GetFileName));

			foreach (Row row in Rows(invoices))
			{
				if (!int.TryParse(row.Text("InvoiceID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
				{
					continue;
				}
				export.Invoices[number] = new AccessInvoice
				{
					Number = number,
					Job = row.Text("ProjectIDFK"),
					Labour = row.Amount("SMADL"),
					Disbursement = row.Amount("Disbursement"),
					Adjustment = row.Amount("Adjustment"),
					Tax = row.Amount("Tax"),
					InvoiceDate = row.Date("InvDate"),
					Paid = row.Flag("Paid"),
					PaymentDate = row.Date("PMTDate"),
					AdjustmentNote = row.Text("AdjNote"),
				};
			}
			foreach (Row row in Rows(clients))
			{
				if (string.IsNullOrWhiteSpace(row.Text("ClientName"))) continue;
				export.Clients.Add(new AccessClient
				{
					Id = row.Text("ClientID"),
					Name = row.Text("ClientName"),
					Inactive = row.Flag("Inactive"),
					Abbreviation = row.Text("NameAbbr"),
				});
			}
			foreach (Row row in Rows(projects))
			{
				if (string.IsNullOrWhiteSpace(row.Text("ProjectID"))) continue;
				export.Projects.Add(new AccessProject
				{
					Status = row.Text("ProjectType"),
					Job = row.Text("ProjectID"),
					Name = row.Text("ProjectName"),
					Client = row.Text("ClientIDFK"),
					Division = row.Text("DivisionIDFK"),
					Po = row.Text("PO"),
					Start = row.Date("StartDate"),
					End = row.Date("EndDate"),
					ProjectManager = row.Text("PMFK"),
					DisbursementRate = row.Has("DisbmtRate") ? row.Amount("DisbmtRate") : null,
					ContractValue = row.Amount("ContractValue"),
					ChangeOrder = row.Amount("ChangeOrder"),
					MajorDisbursement = row.Amount("MajorDisbmt"),
					ProjectDisbursement = row.Amount("ProjectDisbmt"),
					Subconsultant = row.Amount("Subconsultant"),
					TotalBudget = row.Amount("TotalBudget"),
					Notes = row.Text("Notes"),
				});
			}

			string groups = Path.Combine(folder, RateListGroupsFile);
			if (File.Exists(groups))
			{
				export.Files.Add(RateListGroupsFile);
				foreach (string line in File.ReadAllLines(groups))
				{
					string text = line.Trim();
					if (text.Length == 0 || text.StartsWith("#")) continue;
					int colon = text.IndexOf(':');
					string name = colon > 0 ? text[..colon].Trim() : null;
					List<string> jobs = (colon > 0 ? text[(colon + 1)..] : text)
						.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(j => j.Trim())
						.Distinct()
						.ToList();
					if (jobs.Count > 0)
					{
						export.RateListGroups.Add((name, jobs));
					}
				}
			}
			return export;
		}

		// The newest .xlsx in `folder` whose name starts with one of `names`.
		private static string Newest(string folder, params string[] names) =>
			Directory.GetFiles(folder, "*.xlsx")
				.Where(f => names.Any(n => Path.GetFileName(f).StartsWith(n, StringComparison.OrdinalIgnoreCase)))
				.OrderByDescending(File.GetLastWriteTimeUtc)
				.FirstOrDefault();

		// A cell as stored: text, a number (dates are numbers too), or a
		// true/false.
		private record Cell(string Text, double? Number, bool? Flag);

		// A row, its cells by their column's header.
		private class Row
		{
			private readonly Dictionary<string, Cell> _cells;

			public Row(Dictionary<string, Cell> cells) => _cells = cells;

			public bool Has(string column) => _cells.ContainsKey(column);

			public string Text(string column) =>
				_cells.TryGetValue(column, out Cell c)
					? (c.Text ?? c.Number?.ToString(CultureInfo.InvariantCulture) ?? c.Flag?.ToString())?.Trim()
					: null;

			public decimal Amount(string column)
			{
				if (!_cells.TryGetValue(column, out Cell c)) return 0;
				if (c.Number.HasValue) return (decimal)c.Number.Value;
				return decimal.TryParse(c.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 0;
			}

			// Excel keeps a date as its day number; an export may also write
			// it as text.
			public DateOnly? Date(string column)
			{
				if (!_cells.TryGetValue(column, out Cell c)) return null;
				if (c.Number.HasValue) return DateOnly.FromDateTime(DateTime.FromOADate(c.Number.Value));
				return DateTime.TryParse(c.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
					? DateOnly.FromDateTime(d)
					: null;
			}

			// Access's Yes/No: true, "Yes", "True" or -1 (any number but 0).
			public bool Flag(string column)
			{
				if (!_cells.TryGetValue(column, out Cell c)) return false;
				if (c.Flag.HasValue) return c.Flag.Value;
				if (c.Number.HasValue) return c.Number.Value != 0;
				string t = (c.Text ?? "").Trim();
				return t.Equals("yes", StringComparison.OrdinalIgnoreCase)
					|| t.Equals("true", StringComparison.OrdinalIgnoreCase)
					|| t == "-1";
			}
		}

		// The first sheet's rows after the header; none without a file.
		private static List<Row> Rows(string path)
		{
			if (path == null) return new List<Row>();
			using ZipArchive zip = ZipFile.OpenRead(path);
			XDocument Part(string name) =>
				zip.GetEntry(name) is ZipArchiveEntry e ? XDocument.Load(e.Open()) : null;

			List<string> shared = Part("xl/sharedStrings.xml")?.Root
				.Elements(Main + "si")
				.Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value)))
				.ToList() ?? new List<string>();

			// The workbook's first sheet, wherever its file is.
			XElement firstSheet = Part("xl/workbook.xml").Root.Element(Main + "sheets").Elements(Main + "sheet").First();
			string relId = (string)firstSheet.Attribute(Rel + "id");
			string target = (string)Part("xl/_rels/workbook.xml.rels").Root.Elements()
				.First(r => (string)r.Attribute("Id") == relId).Attribute("Target");
			string sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;

			var grid = new List<Dictionary<int, Cell>>();
			foreach (XElement r in Part(sheetPath).Root.Element(Main + "sheetData").Elements(Main + "row"))
			{
				var cells = new Dictionary<int, Cell>();
				foreach (XElement c in r.Elements(Main + "c"))
				{
					string v = c.Element(Main + "v")?.Value;
					Cell cell = (string)c.Attribute("t") switch
					{
						"s" => new Cell(shared[int.Parse(v, CultureInfo.InvariantCulture)], null, null),
						"inlineStr" => new Cell(string.Concat(c.Descendants(Main + "t").Select(t => t.Value)), null, null),
						"str" => new Cell(v, null, null),
						"b" => new Cell(null, null, v == "1"),
						_ => v == null ? null : new Cell(null, double.Parse(v, CultureInfo.InvariantCulture), null),
					};
					if (cell != null)
					{
						cells[ColumnOf((string)c.Attribute("r"))] = cell;
					}
				}
				grid.Add(cells);
			}

			if (grid.Count == 0) return new List<Row>();
			Dictionary<int, string> header = grid[0].ToDictionary(kv => kv.Key, kv => (kv.Value.Text ?? "").Trim());
			return grid.Skip(1)
				.Select(cells => new Row(header
					.Where(h => cells.ContainsKey(h.Key))
					.GroupBy(h => h.Value, StringComparer.OrdinalIgnoreCase)
					.ToDictionary(g => g.Key, g => cells[g.First().Key], StringComparer.OrdinalIgnoreCase)))
				.ToList();
		}

		// "C12" -> 2.
		private static int ColumnOf(string reference)
		{
			int n = 0;
			foreach (char ch in Regex.Match(reference ?? "A", "^[A-Z]+").Value)
			{
				n = n * 26 + ch - 'A' + 1;
			}
			return n - 1;
		}
	}
}
