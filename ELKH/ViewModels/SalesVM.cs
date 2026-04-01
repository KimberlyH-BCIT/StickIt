namespace ELKH.ViewModels;

/// <summary>
/// View model for sales analytics and reporting providing sales metrics,
/// revenue data, and performance indicators for business intelligence dashboards.
/// </summary>
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
    public List<string>  DailyLabels     { get; set; } = [];
    public List<decimal> DailySalesData  { get; set; } = [];

    public List<string>  WeeklyLabels    { get; set; } = [];
    public List<decimal> WeeklySalesData { get; set; } = [];

    public List<string>  MonthlyLabels    { get; set; } = [];
    public List<decimal> MonthlySalesData { get; set; } = [];

    public List<string>  YearlyLabels    { get; set; } = [];
    public List<decimal> YearlySalesData { get; set; } = [];

    // ── Top Products ──────────────────────────────────────────────
    public List<TopProductVM> TopProducts { get; set; } = [];
}

public record TopProductVM(string ProductName = "", int UnitsSold = 0, decimal Revenue = 0M);
