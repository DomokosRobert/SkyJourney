using SkyJourney.Application.Flights;

namespace SkyJourney.UnitTests.Flights;

public sealed class FlightSearchQueryValidatorTests
{
    private static readonly DateOnly Date = new(2026, 11, 2);
    private readonly FlightSearchQueryValidator _validator = new();

    private static FlightSearchQuery Valid() => new() { Origin = "BUD", Destination = "lhr", DepartureDate = Date };

    [Fact]
    public void Minimal_one_way_query_is_valid() => Assert.True(_validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("BU")]
    [InlineData("BUDA")]
    [InlineData("B1D")]
    [InlineData("")]
    public void Origin_must_be_three_letters(string origin)
    {
        var result = _validator.Validate(new FlightSearchQuery { Origin = origin, Destination = "LHR", DepartureDate = Date });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(FlightSearchQuery.Origin));
    }

    [Fact]
    public void Origin_and_destination_must_differ_case_insensitively()
    {
        var result = _validator.Validate(new FlightSearchQuery { Origin = "BUD", Destination = "bud", DepartureDate = Date });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(FlightSearchQuery.Destination));
    }

    [Fact]
    public void Return_before_departure_is_rejected()
    {
        var result = _validator.Validate(new FlightSearchQuery
        {
            Origin = "BUD",
            Destination = "LHR",
            DepartureDate = Date,
            ReturnDate = Date.AddDays(-1),
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(FlightSearchQuery.ReturnDate));
    }

    [Fact]
    public void Same_day_return_is_allowed()
    {
        var result = _validator.Validate(new FlightSearchQuery { Origin = "BUD", Destination = "LHR", DepartureDate = Date, ReturnDate = Date });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 0)]
    [InlineData(5, 5)]
    [InlineData(1, -1)]
    public void Passenger_counts_are_bounded(int adults, int children)
    {
        var result = _validator.Validate(new FlightSearchQuery
        {
            Origin = "BUD",
            Destination = "LHR",
            DepartureDate = Date,
            Adults = adults,
            Children = children,
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Nine_passengers_are_allowed()
    {
        var result = _validator.Validate(new FlightSearchQuery
        {
            Origin = "BUD",
            Destination = "LHR",
            DepartureDate = Date,
            Adults = 2,
            Children = 7,
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("price", FlightSortOrder.Price)]
    [InlineData("DEPARTURE", FlightSortOrder.Departure)]
    [InlineData("Duration", FlightSortOrder.Duration)]
    [InlineData(null, FlightSortOrder.Price)]
    public void Sort_is_parsed_case_insensitively(string? sort, FlightSortOrder expected)
    {
        var query = new FlightSearchQuery { Origin = "BUD", Destination = "LHR", DepartureDate = Date, Sort = sort };

        Assert.True(_validator.Validate(query).IsValid);
        Assert.Equal(expected, query.SortOrder);
    }

    [Theory]
    [InlineData("cheapest")]
    [InlineData("7")]
    public void Unknown_sort_is_rejected(string sort)
    {
        var result = _validator.Validate(new FlightSearchQuery { Origin = "BUD", Destination = "LHR", DepartureDate = Date, Sort = sort });

        Assert.Contains(result.Errors, e => e.PropertyName == "sort");
    }

    [Fact]
    public void Departure_window_must_be_ordered()
    {
        var result = _validator.Validate(new FlightSearchQuery
        {
            Origin = "BUD",
            Destination = "LHR",
            DepartureDate = Date,
            DepartAfter = new TimeOnly(18, 0),
            DepartBefore = new TimeOnly(9, 0),
        });

        Assert.False(result.IsValid);
    }
}
