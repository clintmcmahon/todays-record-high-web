import React, { useState, useEffect } from "react";
import { useSelector } from "react-redux";
import DailyRecords from "../components/daily/DailyRecords";
import MonthNormalObserved from "../components/monthly/MonthNormalObserved";
import * as weatherService from "../services/WeatherDataService";
import Row from "react-bootstrap/Row";
import Col from "react-bootstrap/Col";
import Skeleton from 'react-loading-skeleton';

import TopNav from "../components/navigation/TopNav";
import SetDate from "../components/navigation/SetDate";

function Temperatures() {
    const [dateName, setDateName] = useState("");
    const [normals, setNormals] = useState(null);
    const [records, setRecords] = useState(null);
    const [isLoading, setIsLoading] = useState(true);

    const state = useSelector((state) => state);
    const selectedDate = state.date;

    useEffect(() => {
        setIsLoading(true);

        const month = (selectedDate.getMonth() + 1).toString().padStart(2, "0");
        const day = selectedDate.getDate().toString().padStart(2, "0");
        const year = selectedDate.getFullYear();
        const shortDate = month + "-" + day;
        const longDate = year + "-" + month + "-" + day;
        const dateName =
            selectedDate.toLocaleString("en-US", { month: "long" }) + " " + day;
        setDateName(dateName);

        const fetchNormals = async () => {

            let normals = await weatherService.getNormals(state.location.station, longDate, longDate);
            setNormals(normals);

            setIsLoading(false);
        };

        const fetchRecords = async () => {
            const records = await weatherService.getRecords(state.location.station, shortDate, shortDate);

            setRecords(records);

            setIsLoading(false);
        };

        if (state.location.station) {
            fetchNormals();
            fetchRecords();
        }
    }, [state.location.station, state.date]);

    return (
        <>
            <TopNav />
            <div className="container-fluid">
                <Row>
                    <Col xs={12} className="form-group">
                        <div className="d-sm-flex align-items-center justify-content-between mb-4">
                            {isLoading && (
                                <h1 className="h3 mb-0 text-gray-800">
                                    <Skeleton width={500} />
                                </h1>
                            )}
                            {!isLoading && (
                                <div>

                                    <SetDate />
                                    <div><h1 className="h2 mt-4 text-gray-800">{dateName}</h1> </div>
                                </div>
                            )}</div>
                        <Row>
                            <Col s={6} md={4} className="mb-2">
                                <div className="card border-left-danger shadow h-100 py-2">
                                    <div className="card-body">
                                        <div className="row no-gutters align-items-center">
                                            <div className="col mr-2">
                                                <div className="text-xs font-weight-bold text-danger text-uppercase mb-1">
                                                    Record High{" "}
                                                    {records ? `(${records.highDate.getFullYear()})` : ""}
                                                </div>
                                                {isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        <Skeleton width={100} />
                                                    </div>
                                                )}
                                                {!isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        {records ? `${records.highTemp}℉` : ""}
                                                    </div>
                                                )}
                                            </div>
                                            <div className="col-auto">
                                                <i className="fas fa-temperature-hot fa-2x"></i>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </Col>
                            <Col s={6} md={4} className="mb-2">
                                <div className="card border-left-danger shadow h-100 py-2">
                                    <div className="card-body">
                                        <div className="row no-gutters align-items-center">
                                            <div className="col">
                                                <div className="text-xs font-weight-bold text-danger text-uppercase mb-1">
                                                    Normal High
                                                </div>
                                                {isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        <Skeleton width={100} />
                                                    </div>
                                                )}
                                                {!isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        {normals ? `${normals.high}℉` : ""}
                                                    </div>
                                                )}
                                            </div>
                                            <div className="col-auto">
                                                <i className="fas fa-thermometer-three-quarters fa-2x"></i>
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
                                                    Coldest High{" "}
                                                    {records ? `(${records.coldDate.getFullYear()})` : ""}
                                                </div>
                                                {isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        <Skeleton width={100} />
                                                    </div>
                                                )}
                                                {!isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        {records ? `${records.coldHigh}℉` : ""}
                                                    </div>
                                                )}
                                            </div>
                                            <div className="col-auto">
                                                <i className="fas fa-temperature-up fa-2x"></i>
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
                                                    Record Low{" "}
                                                    {records ? `(${records.lowDate.getFullYear()})` : ""}
                                                </div>
                                                {isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        <Skeleton width={100} />
                                                    </div>
                                                )}
                                                {!isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        {records ? `${records.lowTemp}℉` : ""}
                                                    </div>
                                                )}
                                            </div>
                                            <div className="col-auto">
                                                <i className="fas fa-temperature-frigid fa-2x"></i>
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
                                                    Normal Low
                                                </div>
                                                {isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        <Skeleton width={100} />
                                                    </div>
                                                )}
                                                {!isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        {normals ? `${normals.low}℉` : ""}
                                                    </div>
                                                )}
                                            </div>
                                            <div className="col-auto">
                                                <i className="fas fa-thermometer-quarter fa-2x"></i>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </Col>
                            <Col s={6} md={4} className="mb-2">
                                <div className="card border-left-danger shadow h-100 py-2">
                                    <div className="card-body">
                                        <div className="row no-gutters align-items-center">
                                            <div className="col mr-2">
                                                <div className="text-xs font-weight-bold text-danger text-uppercase mb-1">
                                                    Warmest Low{" "}
                                                    {records ? `(${records.warmDate.getFullYear()})` : ""}
                                                </div>
                                                {isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        <Skeleton width={100} />
                                                    </div>
                                                )}
                                                {!isLoading && (
                                                    <div className="h5 mb-0 font-weight-bold text-gray-800">
                                                        {records ? `${records.warmLow}℉` : ""}
                                                    </div>
                                                )}
                                            </div>
                                            <div className="col-auto">
                                                <i className="fas fa-temperature-down fa-2x "></i>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </Col>
                        </Row>
                        <Row>
                            <Col xs={12} className="mt-4">
                                <DailyRecords
                                    selectedStation={state.location.station ? state.location.station : null}
                                    selectedDate={selectedDate}
                                />
                            </Col>
                        </Row>
                        <Row>
                            <Col xs={12} className="mt-4">
                                <MonthNormalObserved
                                    selectedStation={state.location.station ? state.location.station : null}
                                    selectedDate={selectedDate}
                                />
                            </Col>
                        </Row>
                    </Col>
                </Row>
            </div>
        </>
    )
}

export default Temperatures;
