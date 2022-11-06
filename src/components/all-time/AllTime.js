import React, { useEffect, useState } from "react";
import Row from 'react-bootstrap/Row';
import Col from 'react-bootstrap/Col';

function AllTime() {
  const [currentTemp, setCurrentTemp] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  const dt = new Date();
  const day = dt.getDate().toString().padStart(2, "0");
  const year = dt.getFullYear();
  const month = (dt.getMonth() + 1).toString().padStart(2, "0");
  const shortDate = month + "-" + day;
  const dateName = dt.toLocaleString('en-US', { month: 'long' }) + ' ' + day;

  useEffect(() => {
    setIsLoading(true);
    const fetchRecords = async () => {

      const _query = {
        sid: selectedStation,
        date: year + "-" + month + "-" + day,
        elems: [{ "vX": 23, "prec": 0 }]
      };

      const url = "https://data.rcc-acis.org/StnData";

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        redirect: 'follow',
        body: JSON.stringify(_query)
      });
      const data = await response.json();



      setIsLoading(false);
    }

    fetchRecords();
  }, [selectedStation]);

  return (
    <div className="row no-gutters align-items-center">
      <div className="col mr-2">
        <div className="text-xs font-weight-bold text-danger text-uppercase mb-1">
          Current Temperature</div>
        <div className="h5 mb-0 font-weight-bold text-gray-800">{currentTemp ? `${currentTemp} ℉` : ""}</div>
      </div>
      <div className="col-auto">
        <i className="fas fa-calendar fa-2x text-gray-300"></i>
      </div>
    </div>
  );
}

export default CurrentTemp;