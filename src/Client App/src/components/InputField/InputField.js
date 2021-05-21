import React from 'react';
import { ErrorMessage, useField } from 'formik';
import { Col, FormGroup } from 'react-bootstrap'
import '../../App.css'

export const InputField = ({ label, isLabel = true, ...props }) => {

    const [field, meta] = useField(props);    
    
    switch (props.size) {
        case 50:
            return (
                <Col md={6}>
                    <FormGroup>
                        <label htmlFor={field.name}>{label}</label>
                        <input
                            className={`form-control form-control-sm shadow-none ${meta.touched && meta.error && 'is-invalid'}`}
                            {...field} {...props}
                            autoComplete="off"
                        />
                        <ErrorMessage component="div" name={field.name} className="error" />
                    </FormGroup>
                </Col>

            )

        default:
            return (
                <div className='form-row'>
                    <div className='col-md-12'>
                        <FormGroup>
                            {isLabel && <label htmlFor={field.name}>{label}</label>}
                            <input
                                className={`form-control form-control-sm shadow-none ${meta.touched && meta.error && 'is-invalid'}`}
                                {...field} {...props}
                                autoComplete="off"
                            />
                            <ErrorMessage component="div" name={field.name} className="error" />
                        </FormGroup>
                    </div>
                </div>
            )
    }

}