/* eslint-disable no-unused-vars */
import { Formik, Form , Field} from 'formik';
import React, { useState } from "react";
import { useDispatch, useSelector } from "react-redux";

import { login } from "../../store/auth-actions";
import * as Yup from 'yup';
import { Container, Spinner } from 'react-bootstrap';
import { InputField } from '../../components/InputField/InputField';
import { useHistory } from 'react-router';
import { loading } from '../../utils/stateStatus';

const Login = () => {

    const dispatch = useDispatch();
    const history = useHistory();
    const status = useSelector((state) => state.auth.status);

    const validate = Yup.object({
        userName: Yup.string()
            .required('Username is required'),
        password: Yup.string()
            .min(6, 'Must be 6 characters or more')
            .required('Password is required')
    });

    return (
        <Formik
            initialValues={{
                userName: 'arjuntheguru',
                password: 'Arjun@123'
            }}
            validationSchema={validate}
            onSubmit={values => {

                let userInfo = {

                    username: values.userName,
                    password: values.password

                };

                dispatch(login(userInfo, history));
            }}
        >
            {formik => (
                <Container>
                    {status !== loading ?
                        <div className="row justify-content-center">
                            <div className="col-5">
                                <div
                                    className="card mb-3 shadow border-0"
                                    style={{ marginTop: "10rem" }}
                                >
                                    <>
                                        <div className="card-header text-center lead">Member Login</div>
                                        <Form>
                                            <div className="card-body">

                                                <div className="row no-gutters">
                                                    <InputField size="lg" label="Username" name="userName" type="text" />
                                                    <InputField size="lg" label="Password" name="password" type="password" />
                                                    
                                                </div>
                                                <div className="d-flex justify-content-between">
                                                    <label>
                                                        <Field type="checkbox" name="checked" value="One" />
                                                        Remember me
                                                        </label>
                                                        <a>Forgot Password?</a>
                                                    </div>
                                                    <button
                                                        type="submit"
                                                        className="btn btn-primary btn-block btn-lg mt-3">
                                                        Login
                                                    </button>
                                            </div>
                                        </Form>
                                    </>
                                </div>
                            </div>
                        </div>
                        : <Spinner animation="border spinner" /> }
                </Container>
            )}
        </Formik>

    );
}

export default Login;