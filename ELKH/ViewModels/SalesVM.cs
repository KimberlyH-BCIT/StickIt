namespace ELKH.ViewModels
{
    public class SalesVM
    {
       
            public int WeeklyTotalOrders { get; set; }

            public int MonthlyTotalOrders { get; set; }

            public List<int> WeeklySales { get; set; }

            public List<int> MonthlySales { get; set; }

            public int StockUpCount { get; set; }

            public int StockDownCount { get; set; }

    }
}
