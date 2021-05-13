import React from 'react';
import { Formik, Form } from 'formik';
import { useDispatch } from "react-redux";
import { addSite, getSite, updateSite } from "../../store/site-actions";
import { InputField } from '../../components/InputField/InputField';
import { CustomTimeField } from '../../components/CustomTimeField/CustomTimeField';
import * as Yup from 'yup';

import { Card, Button, Tabs, Tab } from 'react-bootstrap';

const BuisnessHours = [

    { label: "Before Open", name: "beforeOpen" },
    { label: "Open", name: "open" },
    { label: "Morning", name: "morning" },
    { label: "Lunch", name: "lunch" },
    { label: "Afternoon", name: "afternoon" },
    { label: "Evening", name: "evening" },
    { label: "Close", name: "close" },
    { label: "After Close", name: "afterClose" }
]


const SiteForm = ({ isEdit = false, data = {}, onClose, skip }) => {
    const siteInfoValidate = Yup.object({
        name: Yup.string()
            .max(15, 'Must be 15 characters or less')
            .required('Name is required'),
        streetAddress: Yup.string()
            .required('Street Address is required'),
        city: Yup.string()
            .required('City is required'),
        country: Yup.string()
            .required('Country is required'),
        sunTimeFrom: Yup.string()
            .required('Time is required'),
        sunTimeTo: Yup.string()
            .required('Time is required')
    });



    const dispatch = useDispatch();

    return (
        <Formik
            initialValues={{
                id: '',
                name: '',
                streetAddress: '',
                addressLine2: '',
                city: '',
                state: '',
                country: '',
                zip: '',
                sunTimeFrom: '',
                sunTimeTo: ''
            }}
            validationSchema={siteInfoValidate}
            onSubmit={values => {

                let siteInfo = {

                    Id: values.id,
                    Name: values.name,
                    Address: {
                        StreetAddress: values.streetAddress,
                        AddressLine2: values.addressLine2,
                        State: values.state,
                        City: values.city,
                        Country: values.country,
                        ZIP: values.zip
                    }

                };

                if (!isEdit) {
                    console.log("Values")


                    const callback = () => {
                        const pageNumberValue = (skip / 10) + 1;
                        dispatch(getSite({ pageNumber: pageNumberValue, pageSize: 10 }));
                    };

                    dispatch(addSite(siteInfo, callback));
                    onClose();

                }
                else {
                    console.log(values)
                    dispatch(updateSite(siteInfo))
                    onClose();
                }

            }}
        >
            {formik => (
                <div>
                    <Card className="bg-transparent">
                        <Card.Header className='bg-dark text-light text-center  '>{!isEdit ? "Add Site" : "Edit Site"}</Card.Header>
                        <Form>
                            <Card.Body className='card-body'>
                                <InputField label="Name" name="name" readOnly={!isEdit ? false : true} />
                                <Card className='mt-1 bg-transparent'>
                                    <Card.Header className='bg-dark text-light text-center'>Buisness Hours</Card.Header>
                                    <Card.Body>
                                        <Tabs defaultActiveKey="sun" className="mb-3">
                                            <Tab eventKey="sun" title="Sunday">
                                                <>
                                                    {BuisnessHours.map((item) =>
                                                        <CustomTimeField label={item.label} name={item.name + "Sun"} options={["Sun", "Mon"]} />
                                                    )}
                                                </>
                                            </Tab>
                                            <Tab eventKey="mon" title="Monday">
                                                {BuisnessHours.map((item) => [
                                                    <CustomTimeField label={item.label} name={item.name + "Mon"} options={["Mon", "Tue"]} />
                                                ])}
                                            </Tab>
                                            <Tab eventKey="tue" title="Tuesday">
                                                {BuisnessHours.map((item) => [
                                                    <CustomTimeField label={item.label} name={item.name + "Tue"} options={["Tue", "Wed"]} />
                                                ])}
                                            </Tab>
                                            <Tab eventKey="wed" title="Wednesday">
                                                {BuisnessHours.map((item) => [
                                                    <CustomTimeField label={item.label} name={item.name + "Wed"} options={["Wed", "Thu"]} />
                                                ])}
                                            </Tab>
                                            <Tab eventKey="thu" title="Thursday">
                                                {BuisnessHours.map((item) => [
                                                    <CustomTimeField label={item.label} name={item.name + "Thu"} options={["Thu", "Fri"]} />
                                                ])}
                                            </Tab>
                                            <Tab eventKey="fri" title="Friday">
                                                {BuisnessHours.map((item) => [
                                                    <CustomTimeField label={item.label} name={item.name + "Fri"} options={["Fri", "Sat"]} />
                                                ])}
                                            </Tab>
                                            <Tab eventKey="sat" title="Saturday">
                                                {BuisnessHours.map((item) => [
                                                    <CustomTimeField label={item.label} name={item.name + "Sat"} options={["Sat", "Sun"]} />
                                                ])}
                                            </Tab>
                                        </Tabs>
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