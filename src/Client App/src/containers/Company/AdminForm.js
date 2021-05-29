import React from 'react'
import { Formik, Form } from 'formik';
import { useDispatch } from "react-redux";
import { } from "../../store/user-actions";
import { InputField } from '../../components/InputField/InputField';
import * as Yup from 'yup';
import { Card, Button } from 'react-bootstrap';
import { registerUser, getAdmin, updateUser } from "../../store/user-actions";

const AdminForm = ({ isEdit = false, data = {}, onClose, skip }) => {    

    const userInfoValidate = Yup.object({
        firstName: Yup.string()
            .required('First Name is required'),
        lastName: Yup.string()
            .required('Last Name is required'),
        userEmail: Yup.string()
            .email('Email is invalid')
            .required('User Email is required'),
        userName: Yup.string()
            .required('User Name is required')
    });

    const passwordValidate = Yup.object({
        password: Yup.string()
            .min(6, 'Must be 6 characters or more')
            .required('Password is required'),
        confirmPassword: Yup.string()
            .required('Confirm Password is required')
            .oneOf([Yup.ref('password')], 'Passwords must match')
    });

    const dispatch = useDispatch();

    return (
        <Formik
            initialValues={!isEdit ? {
                id: '',
                firstName: '',
                lastName: '',
                userName: '',
                userEmail: '',
                password: '',
                confirmPassword: '',
                companyId: data
            } :
                {
                    id: data.id,
                    firstName: data.firstName,
                    lastName: data.lastName,
                    userName: data.userName,
                    userEmail: data.email,
                    companyId: data.companyId

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
                    Role: "Admin"
                }

                if (!isEdit) {

                    const callback =  () => {
                                          
                        dispatch(getAdmin(data));
                    };
                    
                    dispatch(registerUser(userInfo, callback))                    
                    console.log(values);
                    onClose();
                }
                else {

                    dispatch(updateUser(userInfo));
                    console.log(values);
                    onClose();
                }

            }}
        >
            {formik => (
                <div>
                    <Card className="bg-transparent">
                        <Card.Header className='bg-dark text-light text-center'>{!isEdit ? "Add Admin" : "Edit Admin"}</Card.Header>
                        <Form>
                            <Card className='mt-1 bg-transparent'>
                                <Card.Body>
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

                                </Card.Body>
                                <Card.Footer className='text-right'>
                                    <Button variant="success" type="submit" size="sm">Submit</Button>
                                    <Button variant="danger" className="ml-3" type="reset" size="sm">Reset</Button>
                                </Card.Footer>
                            </Card>
                        </Form>
                    </Card>
                </div>
            )}
        </Formik>
    )
};

export default AdminForm;
