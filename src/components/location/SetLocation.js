import React, { useState, useEffect } from "react";
import { useSelector, useDispatch } from "react-redux";
import { changeLocation } from "../../actions/locations";
import stationData from "../../data/stationData.json";
import * as locationService from "../../services/LocationService";
import Row from "react-bootstrap/Row";
import Col from "react-bootstrap/Col";
import Form from "react-bootstrap/Form";
import Select from "react-select";
import Download from "../app-store/Download";
function SetLocation() {
  const [selectedState, setSelectedState] = useState(null);
  const [selectedStation, setSelectedStation] = useState(null);
  const [stateOptions, setStateOptions] = useState(null);
  const [stationOptions, setStationOptions] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  const state = useSelector((state) => state);
  const dispatch = useDispatch();

  useEffect(() => {
    const _stateOptions = stationData.map((state) => {
      return {
        label: state.name,
        value: state.shortCode,
      };
    });

    setStateOptions(_stateOptions);

    if (state.location) {
      const _selectedState = _stateOptions.find(
        (x) => x.value.toLowerCase() == state.location.state.toLowerCase()
      );
      const _listOfStations = locationService.getStationsByState(
        state.location.state
      );
      const _stationOptions = _listOfStations.map((station) => {
        return {
          label: station.name,
          value: station.sids[0],
        };
      });

      setSelectedState(_selectedState);
      setStationOptions(_stationOptions);
      setSelectedStation(
        _stationOptions.find(
          (stationOption) =>
            stationOption.value.toLowerCase() ===
            state.location.station.toLowerCase()
        )
      );
    } else {
      const _listOfStations = locationService.getStationsByState("AL");
      const _stationOptions = _listOfStations.map((station) => {
        return {
          label: station.name,
          value: station.sids[0],
        };
      });

      setSelectedState(stateOptions[0]);
      setStationOptions(_stationOptions);
      setSelectedStation(_stationOptions[0]);
    }

    setIsLoading(false);
  }, []);

  useEffect(() => {
    if (selectedState || selectedStation) {
      const location = {
        state: selectedState ? selectedState.value : "",
        station: selectedStation ? selectedStation.value : "",
      };

      dispatch(changeLocation(location));
      locationService.setLocation({
        state: selectedState.value,
        station: selectedStation.value,
      });
    }
  }, [selectedState, selectedStation]);

  const setState = (selectedState) => {
    const stateShortCode = selectedState.value;
    const _listOfStations = locationService.getStationsByState(stateShortCode);
    const _stationOptions = _listOfStations.map((station) => {
      return {
        label: station.name,
        value: station.sids[0],
      };
    });
    setSelectedState(selectedState);
    setStationOptions(_stationOptions);
    setSelectedStation(_stationOptions[0]);
  };

  const setStation = (station) => {
    setSelectedStation(station);
  };

  return (
    <Row>
      <Col xs={12}>
        <Form.Group controlId="formBasicEmail">
          <Row>
            <Col md={4} xs={12}>
              <Form.Label>State</Form.Label>
              <Select
                className="wx-select"
                value={selectedState ? selectedState : null}
                onChange={(state) => setState(state)}
                options={stateOptions ? stateOptions : []}
              />
            </Col>
            <Col md={4} xs={12}>
              <Form.Label>Weather Station/Area</Form.Label>
              <Select
                className="wx-select"
                value={!isLoading ? selectedStation : null}
                onChange={(station) => setStation(station)}
                options={stationOptions && !isLoading ? stationOptions : []}
              />
            </Col>
            <Col md={4} xs={12} className="m-auto">
              <Download />
            </Col>
          </Row>
        </Form.Group>
      </Col>
    </Row>
  );
}

export default SetLocation;
