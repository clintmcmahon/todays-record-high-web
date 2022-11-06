import React, { useEffect } from "react";
import Temperatures from "../../screens/Temperatures";
import About from "../../screens/About";
import Privacy from "../../screens/Privacy";

import Row from "react-bootstrap/Row";
import Col from "react-bootstrap/Col";
import { Routes, Route } from "react-router-dom";
import * as locationService from "../../services/LocationService";
import { useSelector, useDispatch } from "react-redux";
import { changeLocation } from "../../actions/locations";
import Sidebar from "./Sidebar";
import "react-datepicker/dist/react-datepicker.css";
import MonthlyRecords from "../../screens/MonthlyRecords";

function RootNavigator() {
  const dispatch = useDispatch();
  const state = useSelector((state) => state);

  useEffect(() => {
    const getLocation = async () => {
      let location = await locationService.getLocation();
      if (location) {
        dispatch(changeLocation(location));
      } else {
        alert("no location found");
      }
    };

    if (!state.location) {
      getLocation();
    }
  }, []);

  if (!state.location) {
    return (
      <Row>
        <Col>
          <div>Loading...</div>
        </Col>
      </Row>
    );
  }
  return (
    <>
      <Sidebar />
      <div id="content-wrapper">
        <div id="content">
          <Routes>
            <Route exact path="/" element={<Temperatures />} />
            <Route path="/about" element={<About />} />
            <Route path="/month" element={<MonthlyRecords />} />
            <Route path="/privacy" element={<Privacy />} />
            <Route path="*" element={<Temperatures />} />
          </Routes>
        </div>
      </div>
    </>
  );
}

export default RootNavigator;
