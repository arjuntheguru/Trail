import React, { useState } from 'react'
import { Formik, Form, Field } from 'formik';
import { useDispatch } from "react-redux";
import { Modal } from 'react-bootstrap';
import { InputField } from '../../components/InputField/InputField';
import * as Yup from 'yup';
import { Card, Button, Col } from 'react-bootstrap';
import { registerUser, getUser, updateUser } from "../../store/user-actions";


import _ from 'lodash';
import { returnLoggedInUser } from '../../store/auth-actions';

import ChangeUserPassword from './ChangeUserPassword';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSave, faTimes } from '@fortawesome/free-solid-svg-icons';

const UserForm = ({ isEdit = false, siteInfo, data, onClose }) => {


    const [showChangePasswordModal, setShowChangePasswordModal] = useState(false);
    const [userName, setUserName] = useState('');

    const changePasswordHandler = (params) => {

        setUserName(params);
        setShowChangePasswordModal(true);
    }

    const changePasswordModalCloseHandler = () => {
        setShowChangePasswordModal(false);
    }

    const dispatch = useDispatch();

    const user = dispatch(returnLoggedInUser());   

    const userInfoValidate = Yup.object({
        firstName: Yup.string()
            .required('First Name is required'),
        lastName: Yup.string()
            .required('Last Name is required'),
        userEmail: Yup.string()
            .email('Email is invalid')
            .required('User Email is required'),
        userName: Yup.string()
            .required('User Name is required'),
        role: Yup.string()
            .required('Role is required')
    });

    const passwordValidate = Yup.object({
        password: Yup.string()
            .min(6, 'Must be 6 characters or more')
            .required('Password is required'),
        confirmPassword: Yup.string()
            .required('Confirm Password is required')
            .oneOf([Yup.ref('password')], 'Passwords must match')
    });

    return (
        <>
            {showChangePasswordModal ? <ChangeUserPassword show={showChangePasswordModal} onClose={changePasswordModalCloseHandler} data={userName} /> : ''}
            <Formik
                initialValues={!isEdit ? {
                    id: '',
                    firstName: '',
                    lastName: '',
                    userName: '',
                    userEmail: '',
                    password: '',
                    confirmPassword: '',
                    companyId: data,
                    role: 'Team User',
                    site: siteInfo.data[0].id
                } :
                    {
                        id: data.id,
                        firstName: data.firstName,
                        lastName: data.lastName,
                        userName: data.userName,
                        userEmail: data.email,
                        companyId: data.companyId,
                        role: data.role,
                        site: data.siteId
                    }}

                validationSchema={!isEdit ? userInfoValidate.concat(passwordValidate) : userInfoValidate}
                onSubmit={values => {

                    const userInfo = {

                        Id: values.id,
                        FirstName: values.firstName,
                        LastName: values.lastName,
                        UserName: values.userName,
                        Email: values.userEmail,
                        Password: values.password,
                        companyId: values.companyId,
                        Role: values.role,
                        SiteId: values.role === "Admin" ? null : _.isArray(values.site) ? values.site : [values.site]

                    }

                    console.log(values);

                    if (!isEdit) {

                        const callback = () => {

                            dispatch(getUser(data));
                        };

                        dispatch(registerUser(userInfo, callback))
                        onClose();
                    }
                    else {

                        dispatch(updateUser(userInfo));
                        onClose();
                    }

                }}
            >
                {({ values }) => (
                    <>
                    <Modal.Header closeButton>{!isEdit ? "Add User" : "Edit User"}</Modal.Header>
                    <Modal.Body>
                            
                            <Form>
                                        <div className='form-row'>
                                            <InputField label="First Name" name="firstName" type="text" size={50} />
                                            <InputField label="Last Name" name="lastName" type="text" size={50} />
                                        </div>
                                        <div className='form-row'>
                                            <InputField label="Username" name="userName" type="text" size={50} />
                                            <InputField label="Email" name="userEmail" type="text" size={50} />
                                        </div>
                                        {!isEdit && <div className='form-row'>
                                            <InputField label="Password" name="password" type="password" size={50} />
                                            <InputField label="Confirm Password" name="confirmPassword" type="password" size={50} />
                                        </div>}
                                        <div className="form-row">
                                           {!_.isEmpty(siteInfo) && 
                                           <>
                                            <Col md={6}>
                                            <div className="form-group">
                                                <label htmlFor="role">Role</label>
                                                <Field as='select' className="form-control shadow-none" name='role' disabled={user.userName === data.userName ? true : false}>
                                                    <option value="Team User">Team User</option>
                                                    <option value="Manager">Manager</option>
                                                    <option value="Admin">Admin</option>
                                                </Field>
                                            </div>
                                        </Col>
                                        <Col md={6}>
                                            <div className="form-group">
                                                <label htmlFor="site">Sites</label>
                                                {values.role !== "Admin" ? (
                                                    <Field as='select' className="form-control shadow-none" name='site' multiple={values.role === "Manager" ? true : false}>
                                                        {siteInfo.data.map(site => {
                                                            return (
                                                                <option value={site.id}>{site.name}</option>
                                                            )
                                                        })}
                                                    </Field>
                                                ) : <p className="text-muted form-control form-control-sm">Admin has acess to all sites</p>}
                                            </div>
                                        </Col> </>}
                                            {isEdit &&
                                            <Button  block variant="outline-dark" onClick={() => changePasswordHandler(data.userName)}>Change Password</Button> }
                                        </div>
                                   
                                        
                                    
                            </Form>
                        
                    </Modal.Body>
                    <Modal.Footer className="justify-content-between">
                    <Button variant="light" type="button" ><FontAwesomeIcon icon={faTimes}/> Cancel</Button>
                    <Button variant="primary" type="submit" ><FontAwesomeIcon icon={faSave}/> Save</Button>
                     
                    </Modal.Footer>
                    </>
                )}
            </Formik>
        </>
    )
};

export default UserForm;
