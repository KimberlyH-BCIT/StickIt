namespace ELKH.ViewModels
{
    public class SalesVM
    {
        // ── Summary Cards ──────────────────────────────────────────
        public decimal WeeklyGrossSales { get; set; }
        public decimal MonthlyGrossSales { get; set; }
        public int WeeklyTotalOrders { get; set; }
        public int MonthlyTotalOrders { get; set; }
        public int TotalOrdersAllTime { get; set; }

        // ── Weekly Chart (last 7 days, day labels + daily totals) ──
        public List<string> WeeklyLabels { get; set; } = new();   // e.g. ["Mon","Tue",...]
        public List<decimal> WeeklySalesData { get; set; } = new();

        // ── Monthly Chart (last 12 months, month labels + totals) ─
        public List<string> MonthlyLabels { get; set; } = new();  // e.g. ["Jan","Feb",...]
        public List<decimal> MonthlySalesData { get; set; } = new();

        // ── Top Products ───────────────────────────────────────────
        public List<TopProductVM> TopProducts { get; set; } = new();
    }

    public class TopProductVM
    {
        public string ProductName { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }
}
