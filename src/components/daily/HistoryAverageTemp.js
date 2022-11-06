import React, { useEffect, useState } from "react";
import Row from 'react-bootstrap/Row';
import Col from 'react-bootstrap/Col';
import Skeleton from 'react-loading-skeleton'
import Highcharts from 'highcharts'
import HighchartsReact from 'highcharts-react-official'
import HighchartsMore from 'highcharts/highcharts-more';
import HC_exporting from 'highcharts/modules/exporting'
import sub from 'date-fns/sub'

HighchartsMore(Highcharts);
HC_exporting(Highcharts);

function HistoryAverageTemp({ selectedStation, selectedDate }) {
  const [temps, setTemps] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const url = "https://data.rcc-acis.org/StnData";


  useEffect(() => {
    setIsLoading(true);
    const fetchRecords = async () => {

      const _query = {
        "sid": selectedStation,
        "elems": [
          {
            "name": "maxt",
            "interval": [
              1,
              0,
              0
            ],
            "duration": 1,
            "smry": [
              {
                "reduce": "max"
              },
              {
                "reduce": "mean"
              },
              {
                "reduce": "min"
              },
              {
                "reduce": "pct_ge_100"
              },
              {
                "reduce": "pct_ge_90"
              },
              {
                "reduce": "pct_ge_50"
              },
              {
                "reduce": "pct_le_32"
              }
            ]
          },
          {
            "name": "mint",
            "interval": [
              1,
              0,
              0
            ],
            "duration": 1,
            "smry": [
              {
                "reduce": "max"
              },
              {
                "reduce": "mean"
              },
              {
                "reduce": "min"
              },
              {
                "reduce": "pct_ge_70"
              },
              {
                "reduce": "pct_ge_50"
              },
              {
                "reduce": "pct_le_32"
              },
              {
                "reduce": "pct_le_0"
              }
            ]
          },
          {
            "name": "pcpn",
            "interval": [
              1,
              0,
              0
            ],
            "duration": 1,
            "smry": [
              {
                "reduce": "max"
              },
              {
                "reduce": "mean"
              },
              {
                "reduce": "min"
              },
              {
                "reduce": "pct_ge_0.01"
              },
              {
                "reduce": "pct_ge_0.10"
              },
              {
                "reduce": "pct_ge_0.50"
              },
              {
                "reduce": "pct_ge_1.0"
              }
            ]
          },
          {
            "name": "snow",
            "interval": [
              1,
              0,
              0
            ],
            "duration": 1,
            "smry": [
              {
                "reduce": "max"
              },
              {
                "reduce": "mean"
              },
              {
                "reduce": "min"
              },
              {
                "reduce": "pct_ge_0.1"
              },
              {
                "reduce": "pct_ge_1.0"
              },
              {
                "reduce": "pct_ge_3.0"
              },
              {
                "reduce": "pct_ge_6.0"
              }
            ]
          },
          {
            "name": "snwd",
            "interval": [
              1,
              0,
              0
            ],
            "duration": 1,
            "smry": [
              {
                "reduce": "max"
              },
              {
                "reduce": "mean"
              },
              {
                "reduce": "min"
              },
              {
                "reduce": "pct_ge_1"
              },
              {
                "reduce": "pct_ge_3"
              },
              {
                "reduce": "pct_ge_6"
              },
              {
                "reduce": "pct_ge_12"
              }
            ]
          }
        ],
        "sDate": "1871-09-21",
        "eDate": "2022-09-21"
      }

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        redirect: 'follow',
        body: JSON.stringify(_query)
      });

      const data = await response.json();

      let categories = [];
      let highsData = [];

      console.log(data)
      if (data && data.data && data.data.length > 0) {
       
        data.data.map((item) => {
          if(!isNaN(item[1]))
          {
          const splitDate = item[0].split("-");
          const year = splitDate[0];
          const high = isNaN(item[1]) ? 0 : parseInt(item[1]);
          const low = isNaN(item[2]) ? 0 : parseInt(item[2]);
          categories.push(year);
          highsData.push(high);
          //lowsData.push([date, low]);
          }
        });
      }

      const options = {
        title: {
          text: `Recent temperatures`
        },

        xAxis: {
          categories: categories
        },

        yAxis: {
          title: {
            text: "Temperature (°F)"
          }
        },

        tooltip: {
          crosshairs: true,
          shared: true,
          valueSuffix: '°F'
        },
        plotOptions: {
          line: {
            dataLabels: {
              enabled: true
            },

          }
        },
        series: [{
          name: 'High',
          color: "#F3453F",
          data: highsData,
          zIndex: 1,
        }]
      }

      console.log(highsData)
      setTemps(options);
      setIsLoading(false);
    }

    if (selectedStation) {
      fetchRecords();
    }
  }, [selectedStation, selectedDate]);

  return (
    <Row>
      <Col xs={12}>
        <div className="card shadow mb-4">
          <div className="card-header py-3 d-flex flex-row align-items-center justify-content-between">
            <h6 className="m-0 font-weight-bold text-primary">Recent temperatures</h6>
          </div>
          <div className="card-body">
            
            {isLoading &&
              <Skeleton count={10} />
            }
            {!isLoading && temps &&
              <HighchartsReact
                highcharts={Highcharts}
                options={temps}
              />
            }
          </div>
        </div>

      </Col>
    </Row>
  );
}

export default HistoryAverageTemp;