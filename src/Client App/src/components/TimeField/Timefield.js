import React from 'react';
import { ErrorMessage, useField } from 'formik';

export const TimeField = ({...props }) => {
    const [field, meta] = useField(props);
    console.log(field)

    return (
        <>
            <input
                className={`form-control form-control-sm shadow-none ${meta.touched && meta.error && 'is-invalid'}`}
                {...field} {...props} type="time"
            />
            <ErrorMessage component="div" name={field.name} className="error" />
        </>
    )
}