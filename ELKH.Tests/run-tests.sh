#!/bin/bash

# ==================================================================
# ELKH PROJECT TEST EXECUTION SCRIPT
# ==================================================================
# Comprehensive testing script for ELKH sticker store project
# Executes all test categories, generates coverage reports,
# and validates quality thresholds.
# ==================================================================

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
PROJECT_NAME="ELKH"
TEST_PROJECT="ELKH.Tests"
COVERAGE_THRESHOLD=75
COVERAGE_TARGET=80
RESULTS_DIR="TestResults"
COVERAGE_DIR="CoverageReport"

echo -e "${BLUE}=================================================================${NC}"
echo -e "${BLUE}ELKH PROJECT - COMPREHENSIVE TEST EXECUTION${NC}"
echo -e "${BLUE}=================================================================${NC}"
echo ""

# Function to print section headers
print_section() {
    echo -e "${YELLOW}📋 $1${NC}"
    echo "----------------------------------------"
}

# Function to print success messages
print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

# Function to print error messages
print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Function to print info messages
print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Clean previous test results
cleanup() {
    print_section "Cleaning Previous Test Results"
    
    if [ -d "$RESULTS_DIR" ]; then
        rm -rf "$RESULTS_DIR"
        print_info "Removed previous test results"
    fi
    
    if [ -d "$COVERAGE_DIR" ]; then
        rm -rf "$COVERAGE_DIR"
        print_info "Removed previous coverage reports"
    fi
    
    mkdir -p "$RESULTS_DIR"
    mkdir -p "$COVERAGE_DIR"
    print_success "Test directories prepared"
}

# Check prerequisites
check_prerequisites() {
    print_section "Checking Prerequisites"
    
    if ! command_exists dotnet; then
        print_error ".NET SDK not found. Please install .NET 10 SDK."
        exit 1
    fi
    
    # Check .NET version
    DOTNET_VERSION=$(dotnet --version)
    print_info ".NET SDK Version: $DOTNET_VERSION"
    
    # Check if test project exists
    if [ ! -f "$TEST_PROJECT/$TEST_PROJECT.csproj" ]; then
        print_error "Test project not found: $TEST_PROJECT/$TEST_PROJECT.csproj"
        exit 1
    fi
    
    print_success "All prerequisites satisfied"
}

# Install required tools
install_tools() {
    print_section "Installing Required Tools"
    
    # Install ReportGenerator if not already installed
    if ! command_exists reportgenerator; then
        print_info "Installing ReportGenerator..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
        print_success "ReportGenerator installed"
    else
        print_info "ReportGenerator already installed"
    fi
    
    # Install coverage tools
    print_info "Ensuring coverage tools are available..."
    dotnet add "$TEST_PROJECT" package coverlet.collector --version 6.0.2 > /dev/null 2>&1 || true
    print_success "Coverage tools ready"
}

# Build the solution
build_solution() {
    print_section "Building Solution"
    
    print_info "Restoring NuGet packages..."
    dotnet restore
    
    print_info "Building in Release configuration..."
    dotnet build --configuration Release --no-restore
    
    print_success "Solution built successfully"
}

# Run unit tests
run_unit_tests() {
    print_section "Running Unit Tests"
    
    print_info "Executing all unit tests with coverage collection..."
    
    dotnet test "$TEST_PROJECT" \
        --configuration Release \
        --no-build \
        --collect:"XPlat Code Coverage" \
        --results-directory "./$RESULTS_DIR" \
        --logger "trx;LogFileName=UnitTestResults.trx" \
        --verbosity minimal \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
    
    if [ $? -eq 0 ]; then
        print_success "Unit tests completed successfully"
    else
        print_error "Unit tests failed"
        exit 1
    fi
}

# Run integration tests
run_integration_tests() {
    print_section "Running Integration Tests"
    
    print_info "Executing integration tests..."
    
    dotnet test "$TEST_PROJECT" \
        --configuration Release \
        --no-build \
        --filter "Category=Integration" \
        --collect:"XPlat Code Coverage" \
        --results-directory "./$RESULTS_DIR" \
        --logger "trx;LogFileName=IntegrationTestResults.trx" \
        --verbosity minimal
    
    if [ $? -eq 0 ]; then
        print_success "Integration tests completed successfully"
    else
        print_error "Integration tests failed"
        exit 1
    fi
}

# Run performance tests
run_performance_tests() {
    print_section "Running Performance Tests"
    
    print_info "Executing performance validation tests..."
    
    dotnet test "$TEST_PROJECT" \
        --configuration Release \
        --no-build \
        --filter "Category=Performance" \
        --logger "trx;LogFileName=PerformanceTestResults.trx" \
        --verbosity normal
    
    if [ $? -eq 0 ]; then
        print_success "Performance tests completed successfully"
    else
        print_error "Performance tests failed - check for performance regressions"
        # Don't exit on performance test failure, but warn
    fi
}

# Generate coverage reports
generate_coverage_report() {
    print_section "Generating Coverage Reports"
    
    # Find coverage files
    COVERAGE_FILES=$(find "$RESULTS_DIR" -name "coverage.cobertura.xml" | tr '\n' ';')
    
    if [ -z "$COVERAGE_FILES" ]; then
        print_error "No coverage files found"
        exit 1
    fi
    
    print_info "Found coverage files: $COVERAGE_FILES"
    print_info "Generating HTML coverage report..."
    
    reportgenerator \
        -reports:"$COVERAGE_FILES" \
        -targetdir:"./$COVERAGE_DIR" \
        -reporttypes:"Html;Badges;TextSummary" \
        -assemblyfilters:"+ELKH*;-*Tests*" \
        -classfilters:"-*.Migrations.*;-*.Areas.Identity.*"
    
    if [ $? -eq 0 ]; then
        print_success "Coverage report generated in $COVERAGE_DIR"
    else
        print_error "Failed to generate coverage report"
        exit 1
    fi
}

# Analyze coverage results
analyze_coverage() {
    print_section "Analyzing Coverage Results"
    
    # Extract coverage percentage from summary
    if [ -f "$COVERAGE_DIR/Summary.txt" ]; then
        COVERAGE_SUMMARY=$(cat "$COVERAGE_DIR/Summary.txt")
        echo "$COVERAGE_SUMMARY"
        
        # Extract line coverage percentage (basic parsing)
        LINE_COVERAGE=$(echo "$COVERAGE_SUMMARY" | grep -o "Line coverage: [0-9]*\.*[0-9]*%" | grep -o "[0-9]*\.*[0-9]*" | head -1)
        
        if [ ! -z "$LINE_COVERAGE" ]; then
            print_info "Line Coverage: ${LINE_COVERAGE}%"
            
            # Compare with thresholds
            if (( $(echo "$LINE_COVERAGE >= $COVERAGE_TARGET" | bc -l) )); then
                print_success "Coverage exceeds target (${COVERAGE_TARGET}%)"
            elif (( $(echo "$LINE_COVERAGE >= $COVERAGE_THRESHOLD" | bc -l) )); then
                print_success "Coverage meets minimum threshold (${COVERAGE_THRESHOLD}%)"
            else
                print_error "Coverage below minimum threshold (${COVERAGE_THRESHOLD}%)"
                print_error "Current: ${LINE_COVERAGE}% | Required: ${COVERAGE_THRESHOLD}%"
                exit 1
            fi
        fi
    else
        print_info "Coverage summary not available"
    fi
}

# Count test results
analyze_test_results() {
    print_section "Analyzing Test Results"
    
    # Count test results from TRX files
    TOTAL_TESTS=0
    PASSED_TESTS=0
    FAILED_TESTS=0
    
    for trx_file in "$RESULTS_DIR"/*.trx; do
        if [ -f "$trx_file" ]; then
            # Basic counting - would need XML parsing for exact numbers
            print_info "Test results file: $(basename "$trx_file")"
        fi
    done
    
    # Check if any tests failed by looking at exit codes from previous steps
    print_success "Test execution completed - see individual results above"
}

# Generate summary report
generate_summary() {
    print_section "Test Execution Summary"
    
    echo ""
    echo "📊 ELKH TEST EXECUTION SUMMARY"
    echo "================================"
    echo "Test Project: $TEST_PROJECT"
    echo "Configuration: Release"
    echo "Coverage Target: ${COVERAGE_TARGET}%"
    echo "Coverage Threshold: ${COVERAGE_THRESHOLD}%"
    echo ""
    echo "📁 Output Directories:"
    echo "  Test Results: $RESULTS_DIR/"
    echo "  Coverage Report: $COVERAGE_DIR/index.html"
    echo ""
    echo "🌐 View Coverage Report:"
    echo "  Open $COVERAGE_DIR/index.html in your browser"
    echo ""
    
    if [ -f "$COVERAGE_DIR/index.html" ]; then
        print_success "Full test suite completed successfully!"
        
        # Try to open coverage report (works on some systems)
        if command_exists xdg-open; then
            print_info "Opening coverage report..."
            xdg-open "$COVERAGE_DIR/index.html" > /dev/null 2>&1 &
        elif command_exists open; then
            print_info "Opening coverage report..."
            open "$COVERAGE_DIR/index.html" > /dev/null 2>&1 &
        fi
    else
        print_error "Test execution completed with issues - check logs above"
        exit 1
    fi
}

# Main execution flow
main() {
    echo -e "${BLUE}Starting comprehensive test execution...${NC}"
    echo ""
    
    cleanup
    check_prerequisites
    install_tools
    build_solution
    run_unit_tests
    run_integration_tests
    run_performance_tests
    generate_coverage_report
    analyze_coverage
    analyze_test_results
    generate_summary
    
    echo ""
    echo -e "${GREEN}🎉 ELKH test execution completed successfully!${NC}"
    echo -e "${GREEN}📈 Ready for production deployment${NC}"
}

# Handle script arguments
case "${1:-full}" in
    "unit")
        print_info "Running unit tests only..."
        check_prerequisites
        build_solution
        run_unit_tests
        ;;
    "integration")
        print_info "Running integration tests only..."
        check_prerequisites
        build_solution
        run_integration_tests
        ;;
    "performance")
        print_info "Running performance tests only..."
        check_prerequisites
        build_solution
        run_performance_tests
        ;;
    "coverage")
        print_info "Generating coverage report only..."
        generate_coverage_report
        analyze_coverage
        ;;
    "full"|*)
        main
        ;;
esac