import React from 'react';
import { Formik, Form, FieldArray } from 'formik';
import { useDispatch, useSelector } from "react-redux";
import { addSite, getSite, updateSite } from "../../store/site-actions";
import { InputField } from '../../components/InputField/InputField';
import { CustomTimeField } from '../../components/CustomTimeField/CustomTimeField';
import * as Yup from 'yup';

import { Card, Button, Tabs, Tab } from 'react-bootstrap';


const SiteForm = ({ isEdit = false, data = {}, onClose, skip }) => {

    const auth = useSelector(state => state.auth)

    const WeekDays = [
        { eventKey: 'sun', title: 'Sunday', options: [{ name: 'Sun', value: 0 }, { name: 'Mon', value: 1 }] },
        { eventKey: 'mon', title: 'Monday', options: [{ name: 'Mon', value: 1 }, { name: 'Tue', value: 2 }] },
        { eventKey: 'tue', title: 'Tuesday', options: [{ name: 'Tue', value: 2 }, { name: 'Wed', value: 3 }] },
        { eventKey: 'wed', title: 'Wednesday', options: [{ name: 'Wed', value: 3 }, { name: 'Thu', value: 4 }] },
        { eventKey: 'thu', title: 'Thursday', options: [{ name: 'Thu', value: 4 }, { name: 'Fri', value: 5 }] },
        { eventKey: 'fri', title: 'Friday', options: [{ name: 'Fri', value: 5 }, { name: 'Sat', value: 6 }] },
        { eventKey: 'sat', title: 'Saturday', options: [{ name: 'Sat', value: 6 }, { name: 'Sun', value: 0 }] },
    ]

    const initialValues = {
        id: '',
        name: '',
        streetAddress: '',
        addressLine2: '',
        city: '',
        state: '',
        country: '',
        zip: '',
        buisnessHours: [

        ]
    };

    const initialValuesEdited = {
        id : data.id,        
        name: data.name,
        streetAddress: data.address ? data.address.streetAddress : '',
        addressLine2: data.address ? data.address.addressLine2 : '',
        city: data.address ? data.address.city : '',
        state: data.address ? data.address.state : '',
        country: data.address ? data.address.country : '',
        zip: data.address ? data.address.zip: '',
        buisnessHours: data.buisnessHours
    }

    for (var i = 0; i < 7; i++) {
        initialValues.buisnessHours.push({
            beforeOpenFrom: { day: i, time: '' },
            beforeOpenTo: { day: i, time: '' },
            openFrom: { day: i, time: '' },
            openTo: { day: i, time: '' },
            morningFrom: { day: i, time: '' },
            morningTo: { day: i, time: '' },
            lunchFrom: { day: i, time: '' },
            lunchTo: { day: i, time: '' },
            afternoonFrom: { day: i, time: '' },
            afternoonTo: { day: i, time: '' },
            eveningFrom: { day: i, time: '' },
            eveningTo: { day: i, time: '' },
            closeFrom: { day: i, time: '' },
            closeTo: { day: i, time: '' },
            afterCloseFrom: { day: i, time: '' },
            afterCloseTo: { day: i, time: '' }
        });
    }

    const siteInfoValidate = Yup.object().shape({
        name: Yup.string()
            .max(15, 'Must be 15 characters or less')
            .required('Name is required'),
        buisnessHours: Yup.array().of(
            Yup.object().shape({
                day: Yup.number().required("Required field"),
                beforeOpenFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                beforeOpenTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                openFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                openTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                morningFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                morningTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                lunchFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                lunchTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                afternoonFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                afternoonTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                eveningFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                eveningTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                closeFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                closeTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                afterCloseFrom: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                }),
                afterCloseTo: Yup.object().shape({
                    day: Yup.number().required("Required field"),
                    time: Yup.string().required("Required field"),
                })
            })
        )
    });

    const setBuisnessHourValues = (values, setValues) => {

        var buisnessHours = []

        for (var i = 0; i < 7; i++) {
            buisnessHours.push({
                day: i,
                beforeOpenFrom: { day: i, time: '03:00' },
                beforeOpenTo: { day: i, time: '11:00' },
                openFrom: { day: i, time: '11:00' },
                openTo: { day: i, time: '11:30' },
                morningFrom: { day: i, time: '11:30' },
                morningTo: { day: i, time: '12:00' },
                lunchFrom: { day: i, time: '12:00' },
                lunchTo: { day: i, time: '14:30' },
                afternoonFrom: { day: i, time: '14:30' },
                afternoonTo: { day: i, time: '17:30' },
                eveningFrom: { day: i, time: '17:30' },
                eveningTo: { day: i, time: '21:00' },
                closeFrom: { day: i, time: '21:00' },
                closeTo: { day: i + 1 > 6 ? 0 : i + 1, time: '00:00' },
                afterCloseFrom: { day: i + 1 > 6 ? 0 : i + 1, time: '00:00' },
                afterCloseTo: { day: i + 1 > 6? 0 : i + 1, time: '03:00' }
            });
        }

        setValues({ ...values, buisnessHours });
    }

    const dispatch = useDispatch();

    return (
        <Formik
            initialValues={!isEdit ? initialValues : initialValuesEdited}
            validationSchema={siteInfoValidate}
            onSubmit={values => {

                let siteInfo = {

                    Id: values.id,
                    Name: values.name,
                    CompanyId : auth.user.companyId,
                    Address: {
                        StreetAddress: values.streetAddress,
                        AddressLine2: values.addressLine2,
                        State: values.state,
                        City: values.city,
                        Country: values.country,
                        ZIP: values.zip
                    },
                    BuisnessHours: values.buisnessHours

                };

                if (!isEdit) {

                    const callback =  () => {
                        const pageNumberValue = (skip / 10) + 1;                   
                        dispatch(getSite({ pageNumber: pageNumberValue, pageSize: 10 }));
                    };
                    console.log(siteInfo);
                    // dispatch(addSite(siteInfo, callback));
                    onClose();                
                    
                }
                else {
                    console.log(values)
                    dispatch(updateSite(siteInfo))
                    onClose();
                }                
            }}
        >
            {({ values, setValues, setFieldValue }) => (
                <div>                    
                    <Card className="bg-transparent">
                        <Card.Header className='bg-dark text-light text-center'>{!isEdit ? "Add Site" : "Edit Site"}</Card.Header>
                        <Form>
                            <Card.Body className='card-body'>
                                <InputField label="Name" name="name" readOnly={!isEdit ? false : true} />
                                <Card className='mt-1 bg-transparent'>
                                    <Card.Header className='bg-dark text-light text-center'>Buisness Hours
                                    </Card.Header>
                                    <Card.Body>
                                        <FieldArray name="buisnessHours">
                                            <>
                                                <div className="text-center">
                                                    <Button type="button" size="sm" variant="outline-dark" className="w-100 mb-3" onClick={() => { setBuisnessHourValues(values, setValues) }}>Set Default Value</Button>

                                                </div>
                                                <Tabs defaultActiveKey="sun" className="mb-3">                                                    
                                                    {(values.buisnessHours.map((buisnessHour, i) => {
                                                        return (
                                                            <Tab eventKey={WeekDays[i].eventKey} title={WeekDays[i].title}>
                                                                <>
                                                                    <input name={`buisnessHours.${i}.day`} type="hidden"/>
                                                                    <CustomTimeField label='Before Open' name={`buisnessHours.${i}.beforeOpen`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                    <CustomTimeField label='Open' name={`buisnessHours.${i}.open`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                    <CustomTimeField label='Morning' name={`buisnessHours.${i}.morning`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                    <CustomTimeField label='Lunch' name={`buisnessHours.${i}.lunch`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                    <CustomTimeField label='Afternoon' name={`buisnessHours.${i}.afternoon`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                    <CustomTimeField label='Evening' name={`buisnessHours.${i}.evening`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                    <CustomTimeField label='Close' name={`buisnessHours.${i}.close`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                    <CustomTimeField label='After Close' name={`buisnessHours.${i}.afterClose`} options={WeekDays[i].options} setFieldValue={setFieldValue}/>
                                                                </>
                                                            </Tab>
                                                        )
                                                    }))}
                                                </Tabs>

                                            </>
                                        </FieldArray>
                                    </Card.Body>
                                </Card>
                                <Card className='mt-1 bg-transparent'>
                                    <Card.Header className='bg-dark text-light text-center'>Address (optional)</Card.Header>
                                    <Card.Body>
                                        <div className='form-row'>
                                            <InputField label="Street Address" name="streetAddress" type="text" size={50} />
                                            <InputField label="Address Line 2" name="addressLine2" type="text" size={50} />
                                        </div>
                                        <div className='form-row'>
                                            <InputField label="City" name="city" type="text" size={50} />
                                            <InputField label="State" name="state" type="text" size={50} />
                                        </div>
                                        <div className='form-row'>
                                            <InputField label="Country" name="country" type="text" size={50} />
                                            <InputField label="ZIP" name="zip" type="text" size={50} />
                                        </div>
                                    </Card.Body>
                                </Card>
                            </Card.Body>
                            <Card.Footer className='text-right'>
                                <Button variant="success" type="submit" size="sm">Submit</Button>
                                <Button variant="danger" className="ml-3" type="reset" size="sm">Reset</Button>
                            </Card.Footer>
                        </Form>
                    </Card>
                </div>
            )}
        </Formik>
    )
}


export default SiteForm;