import { Formik, Form } from 'formik';
import { useDispatch } from "react-redux";

import { InputField } from '../../components/InputField/InputField';
import * as Yup from 'yup';
import { Card, Button } from 'react-bootstrap';
import { changePassword } from '../../store/user-actions';

const ChangePasswordForm = ({data , onClose}) => {

    const validate = Yup.object({
        
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
            initialValues={
                {                    
                    password: '',
                    confirmPassword: ''
                }
            }
            validationSchema={validate}
            onSubmit={values => {
                const authenticateModel = {

                    UserName : data,
                    Password : values.password
                }
                
                dispatch(changePassword(authenticateModel));
                onClose();
            }}
        >
            {formik => (
                <>
                    <Form>
                        <Card className='mt-1 bg-transparent'>
                            <Card.Header className='bg-dark text-light text-center'>Change Password</Card.Header>
                            <Card.Body>
                                <div className='form-row'>
                                    <InputField label="Password" name="password" type="password" size={50} />
                                    <InputField label="Confirm Password" name="confirmPassword" type="password" size={50} />
                                </div>
                            </Card.Body>
                            <Card.Footer className='text-right'>
                                <Button variant="success" type="submit" size="sm">Submit</Button>
                                <Button variant="danger" className="ml-3" type="reset" size="sm">Reset</Button>
                            </Card.Footer>
                        </Card>
                    </Form>
                </>
            )}

        </Formik>
    );
};

export default ChangePasswordForm;