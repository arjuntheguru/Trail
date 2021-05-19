import React from 'react';
import { Col, Row } from 'react-bootstrap'
import { Field } from 'formik';
import { TimeField } from '../TimeField/Timefield';

export const CustomTimeField = ({ label, ...props }) => {

    return (
        <Row className="my-3">
            <Col md={3} className="m-0">
                <label htmlFor={props.name}>{label}</label>
            </Col>
            <Col md={2} className="pl-1">
                <Field as='select' className="form-control form-control-sm shadow-none" name={props.name + 'From.day'} 
                onChange={(e) => 
                  props.setFieldValue(props.name + 'From.day', parseInt(e.target.value))
                }>
                    {props.options.map((option) =>
                        <option value={option.value}>{option.name}</option>
                    )}
                </Field>               
            </Col>
            <Col md={2} className="p-0">
                <TimeField name={props.name + 'From.time'} />
            </Col>
            <Col md={1} className="pl-4">
                <span>to</span>
            </Col>
            <Col md={2} className="pl-1">
                <Field as='select' className="form-control form-control-sm shadow-none" name={props.name + 'To.day'}
                onChange={(e) => 
                    props.setFieldValue(props.name + 'To.day', parseInt(e.target.value))
                  }>
                    {props.options.map((option) =>
                        <option value={option.value}>{option.name}</option>
                    )}
                </Field>
            </Col>
            <Col md={2} className="p-0">
                <TimeField name={props.name + 'To.time'} />
            </Col>
        </Row>
    )
}