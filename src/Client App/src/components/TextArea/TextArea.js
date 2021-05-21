import React from 'react';
import { ErrorMessage, useField } from 'formik';
import { FormGroup } from 'react-bootstrap'
import '../../App.css'

export const TextArea = ({ label, ...props }) => {

    const [field, meta] = useField(props);    

    return (
        <div className='form-row'>
            <div className='col-md-12'>
                <FormGroup>
                    <label htmlFor={field.name}>{label}</label>
                    <textarea
                        className={`form-control form-control-sm shadow-none ${meta.touched && meta.error && 'is-invalid'}`}
                        {...field} {...props}                        
                    />
                    <ErrorMessage component="div" name={field.name} className="error" />
                </FormGroup>
            </div>
        </div>
    )

}