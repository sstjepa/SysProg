using System.Text.Json.Serialization;

public class NobelApiResponse
{
    [JsonPropertyName("nobelPrizes")]
    public List<NobelPrize> NobelPrizes { get; set; }
}


public class NobelPrize
{
    [JsonPropertyName("awardYear")]
    public string AwardYear { get; set; }

    [JsonPropertyName("category")]
    public Category Category { get; set; }

    [JsonPropertyName("prizeAmountAdjusted")]
    public int PrizeAmountAdjusted { get; set; }

    [JsonPropertyName("laureates")]
    public List<Laureate> Laureates { get; set; }
}

public class Category
{
    [JsonPropertyName("en")]
    public string En { get; set; }
}

public class Laureate
{
    [JsonPropertyName("knownName")]
    public KnownName KnownName { get; set; }
}

public class KnownName
{
    [JsonPropertyName("en")]
    public string En { get; set; }
}