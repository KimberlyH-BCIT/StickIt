document.addEventListener("DOMContentLoaded", function() {

    console.log("Charts loaded");

    if (!window.chartData) {
        console.error("No chart data found");
        return;
    }

    const d = window.chartData;

    function createChart(id, type, labels, data, labelName) {
        new Chart(document.getElementById(id), {
            type: type,
            data: {
                labels: labels,
                datasets: [{
                    label: labelName,
                    data: data,
                    backgroundColor: "rgba(54, 162, 235, 0.5)",
                    borderColor: "rgba(54, 162, 235, 1)",
                    borderWidth: 2,
                    fill: type === "line"
                }]
            }
        });
    }

    createChart("dailyChart", "bar", d.dailyLabels, d.dailySales, "Daily Sales");
    createChart("weeklyChart", "bar", d.weeklyLabels, d.weeklySales, "Weekly Sales");
    createChart("monthlyChart", "line", d.monthlyLabels, d.monthlySales, "Monthly Sales");
    createChart("yearlyChart", "bar", d.yearlyLabels, d.yearlySales, "Yearly Sales");
    createChart("topProductsChart", "bar", d.topNames, d.topUnits, "Units Sold");

});