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

function MonthNormalObserved({ selectedStation, selectedDate }) {
  const [temps, setTemps] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [daysAboveAverage, setDaysAboveAverage] = useState(null);
  const [daysBelowAverage, setDaysBelowAverage] = useState(null);

  const endDay = selectedDate.getDate().toString().padStart(2, "0");
  const endMonth = (selectedDate.getMonth() + 1).toString().padStart(2, "0");
  const endYear = selectedDate.getFullYear();
  const startDate = sub(selectedDate, {
    days: 30
  });
  const startDay = startDate.getDate().toString().padStart(2, "0");
  const startMonth = (startDate.getMonth() + 1).toString().padStart(2, "0");
  const startYear = startDate.getFullYear();
  const url = "https://data.rcc-acis.org/StnData";
  

  useEffect(() => {
    setIsLoading(true);
    const fetchRecords = async () => {

      const _query = {
        sid: selectedStation,
        elems: [
          { name: "maxt" },
          { name: "mint" },
          { name: "maxt", "duration": "dly", "normal": "91", "prec": 1 },
          { name: "mint", "duration": "dly", "normal": "91", "prec": 1 }
        ],
        sid: selectedStation,
        "sDate": startYear + "-" + startMonth + "-" + startDay,
        "eDate": endYear + "-" + endMonth + "-" + endDay
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

      let highsData = [];
      let lowsData = [];
      let normalsData = [];
      let categories = [];
      let daysAboveAverage = 0;
      let daysBelowAverage = 0;

      if (data && data.data && data.data.length > 0) {
        data.data.map((item) => {
          const splitDate = item[0].split("-");
          const date = splitDate[1] + "-" + splitDate[2];
          const high = parseFloat(item[1]);
          const low = parseFloat(item[2]);
          const normalHigh = parseFloat(item[3]);
          const normalLow = parseFloat(item[4]);

          if (high > normalHigh) {
            daysAboveAverage++;
          }

          if (high < normalHigh) {
            daysBelowAverage++;
          }

          categories.push(date);
          highsData.push([date, high]);
          lowsData.push([date, low]);
          normalsData.push([date, normalHigh, normalLow]);
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

        },
        {
          name: 'Low',
          color: "#2A4473",
          data: lowsData,
          zIndex: 1,
        },
        {
          name: "Normal Range",
          data: normalsData,
          type: 'arearange',
          lineWidth: 0,
          fillOpacity: 0.3,
          zIndex: 0,
          marker: {
            enabled: false
          }
        }]
      }

      setTemps(options);
      setDaysAboveAverage(daysAboveAverage);
      setDaysBelowAverage(daysBelowAverage);
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
            <Row>
              <Col s={6} md={4} className="mb-2">
                <div className="card border-left-danger shadow h-100 py-2">
                  <div className="card-body">
                    <div className="row no-gutters align-items-center">
                      <div className="col mr-2">
                        <div className="text-xs font-weight-bold text-danger text-uppercase mb-1">
                          Days above normal
                        </div>
                        {isLoading &&
                          <div className="h5 mb-0 font-weight-bold text-gray-800"><Skeleton width={100} /></div>
                        }
                        {!isLoading &&
                          <div className="h5 mb-0 font-weight-bold text-gray-800">{daysAboveAverage ? daysAboveAverage : ""}</div>
                        }
                      </div>
                      <div className="col-auto">
                        <i className="fas fa-temperature-hot fa-2x"></i>
                      </div>
                    </div>
                  </div>
                </div>
              </Col>
              <Col s={6} md={4} className="mb-2">
                <div className="card border-left-primary shadow h-100 py-2">
                  <div className="card-body">
                    <div className="row no-gutters align-items-center">
                      <div className="col mr-2">
                        <div className="text-xs font-weight-bold text-primary text-uppercase mb-1">
                          Days below normal
                        </div>
                        {isLoading &&
                          <div className="h5 mb-0 font-weight-bold text-gray-800"><Skeleton width={100} /></div>
                        }
                        {!isLoading &&
                          <div className="h5 mb-0 font-weight-bold text-gray-800">{daysBelowAverage ? daysBelowAverage : ""}</div>
                        }
                      </div>
                      <div className="col-auto">
                        <i className="fas fa-temperature-frigid fa-2x"></i>
                      </div>
                    </div>
                  </div>
                </div>
              </Col>
            </Row>
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

export default MonthNormalObserved;