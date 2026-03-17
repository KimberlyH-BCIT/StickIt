namespace ELKH.ViewModels
{
    public class SalesVM
    {
        // ── Stock KPIs (used by Admin Index) ──────────────────────────
        public int StockUpCount   { get; set; }
        public int StockDownCount { get; set; }

        // ── Summary Numbers ───────────────────────────────────────────
        public decimal DailyGrossSales    { get; set; }
        public decimal WeeklyGrossSales   { get; set; }
        public decimal MonthlyGrossSales  { get; set; }
        public decimal YearlyGrossSales   { get; set; }

        public int DailyTotalOrders    { get; set; }
        public int WeeklyTotalOrders   { get; set; }
        public int MonthlyTotalOrders  { get; set; }
        public int YearlyTotalOrders   { get; set; }
        public int TotalOrdersAllTime  { get; set; }

        // ── Chart Data ────────────────────────────────────────────────
        public List<string>  DailyLabels     { get; set; } = new();
        public List<decimal> DailySalesData  { get; set; } = new();

        public List<string>  WeeklyLabels    { get; set; } = new();
        public List<decimal> WeeklySalesData { get; set; } = new();

        public List<string>  MonthlyLabels    { get; set; } = new();
        public List<decimal> MonthlySalesData { get; set; } = new();

        public List<string>  YearlyLabels    { get; set; } = new();
        public List<decimal> YearlySalesData { get; set; } = new();

        // ── Top Products ──────────────────────────────────────────────
        public List<TopProductVM> TopProducts { get; set; } = new();
    }

    public class TopProductVM
    {
        public string  ProductName { get; set; } = string.Empty;
        public int     UnitsSold   { get; set; }
        public decimal Revenue     { get; set; }
    }
}
