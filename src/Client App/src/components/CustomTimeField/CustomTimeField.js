import React from 'react';
import { Col, Row } from 'react-bootstrap'
import { TimeField } from '../TimeField/Timefield';

export const CustomTimeField = ({ label, ...props }) => {

    return (
        <Row>
            <Col md={3} className="m-0">
                <label htmlFor={props.name}>{label}</label>
            </Col>
            <Col md={2} className="pl-1">
                <select className="form-control form-control-sm shadow-none">
                    {props.options.map((option) =>
                        <option value={option}>{option}</option>
                    )}
                </select>
            </Col>
            <Col md={2} className="p-0">
                <TimeField name={props.name + 'From'} />
            </Col>
            <Col md={1} className="pl-4">
                <span>to</span>
            </Col>
            <Col md={2} className="pl-1">
                <select className="form-control form-control-sm shadow-none">
                    {props.options.map((option) =>
                        <option value={option}>{option}</option>
                    )}
                </select>
            </Col>
            <Col md={2} className="p-0">
                <TimeField name={props.name + 'To'} />
            </Col>
        </Row>
    )
}