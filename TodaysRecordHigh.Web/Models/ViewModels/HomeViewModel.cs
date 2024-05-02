using System.Data;
using TodaysRecordHigh.Web.Models;
using TodaysRecordHigh.Web.Models.ACIS;
namespace TodaysRecordHigh.Web.Models.ViewModels;

public class HomeViewModel
{

    public HomeViewModel()
    {
        States = new List<State>
        {
            new State { Label = "Alabama", Value = "AL" },
            new State { Label = "Alaska", Value = "AK" },
            new State { Label = "American Samoa", Value = "AS" },
            new State { Label = "Arizona", Value = "AZ" },
            new State { Label = "Arkansas", Value = "AR" },
            new State { Label = "California", Value = "CA" },
            new State { Label = "Colorado", Value = "CO" },
            new State { Label = "Connecticut", Value = "CT" },
            new State { Label = "Delaware", Value = "DE" },
            new State { Label = "District Of Columbia", Value = "DC" },
            new State { Label = "Federated States Of Micronesia", Value = "FM" },
            new State { Label = "Florida", Value = "FL" },
            new State { Label = "Georgia", Value = "GA" },
            new State { Label = "Guam", Value = "GU" },
            new State { Label = "Hawaii", Value = "HI" },
            new State { Label = "Idaho", Value = "ID" },
            new State { Label = "Illinois", Value = "IL" },
            new State { Label = "Indiana", Value = "IN" },
            new State { Label = "Iowa", Value = "IA" },
            new State { Label = "Kansas", Value = "KS" },
            new State { Label = "Kentucky", Value = "KY" },
            new State { Label = "Louisiana", Value = "LA" },
            new State { Label = "Maine", Value = "ME" },
            new State { Label = "Marshall Islands", Value = "MH" },
            new State { Label = "Maryland", Value = "MD" },
            new State { Label = "Massachusetts", Value = "MA" },
            new State { Label = "Michigan", Value = "MI" },
            new State { Label = "Minnesota", Value = "MN" },
            new State { Label = "Mississippi", Value = "MS" },
            new State { Label = "Missouri", Value = "MO" },
            new State { Label = "Montana", Value = "MT" },
            new State { Label = "Nebraska", Value = "NE" },
            new State { Label = "Nevada", Value = "NV" },
            new State { Label = "New Hampshire", Value = "NH" },
            new State { Label = "New Jersey", Value = "NJ" },
            new State { Label = "New Mexico", Value = "NM" },
            new State { Label = "New York", Value = "NY" },
            new State { Label = "North Carolina", Value = "NC" },
            new State { Label = "North Dakota", Value = "ND" },
            new State { Label = "Northern Mariana Islands", Value = "MP" },
            new State { Label = "Ohio", Value = "OH" },
            new State { Label = "Oklahoma", Value = "OK" },
            new State { Label = "Oregon", Value = "OR" },
            new State { Label = "Palau", Value = "PW" },
            new State { Label = "Pennsylvania", Value = "PA" },
            new State { Label = "Puerto Rico", Value = "PR" },
            new State { Label = "Rhode Island", Value = "RI" },
            new State { Label = "South Carolina", Value = "SC" },
            new State { Label = "South Dakota", Value = "SD" },
            new State { Label = "Tennessee", Value = "TN" },
            new State { Label = "Texas", Value = "TX" },
            new State { Label = "Utah", Value = "UT" },
            new State { Label = "Vermont", Value = "VT" },
            new State { Label = "Virgin Islands", Value = "VI" },
            new State { Label = "Virginia", Value = "VA" },
            new State { Label = "Washington", Value = "WA" },
            new State { Label = "West Virginia", Value = "WV" },
            new State { Label = "Wisconsin", Value = "WI" },
            new State { Label = "Wyoming", Value = "WY" }
        };

    }
    public List<State> States { get; set; }
    public WeatherRecords WeatherRecords { get; set; }
    public WeatherNormals WeatherNormals { get; set; }
    public MonthNormalObserved MonthNormalObserved { get; set; }
    public WeatherResponse DailyHistory { get; set; }
    public bool IsDefault { get; set; }
    public dynamic StationData { get; set; }
    public string SelectedStateName { get; set; }
    public string SelectedStationName { get; set; }
}
