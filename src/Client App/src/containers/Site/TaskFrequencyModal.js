import React, { useState } from 'react';
import { Modal, Button, Card } from 'react-bootstrap';
import { Formik, Form, Field } from 'formik';

const TaskFrequencyModal = ({ show, onClose, callback }) => {     

    const [isWeekly, setIsWeekly] = useState(false);

    const radioValueChangeHandler = (e) => {
       console.log(e)
        if (e.target.defaultValue === "2") {
            setIsWeekly(true)
        }
        else {
            setIsWeekly(false)
        }
    }


    return (
        <Modal show={show} onHide={onClose} centered size="sm" >
            <Modal.Body className="text-center bgGradient">
                <Card>
                    <Card.Header className="bg-dark text-light">
                        Task Frequency
                    </Card.Header>
                    <Formik
                        initialValues={{
                            taskFrequency: "1",
                            dueBy: "00:00:00",
                            weeklyRepetition: 0
                        }}
                        onSubmit={values => {
                            callback(values);
                        }}
                    >
                        {formik => (
                            <div>
                                <Form>
                                    <Card.Body>
                                       <div role="group" onChange={e => radioValueChangeHandler(e)}>
                                        <label>
                                                <Field type="radio" name="taskFrequency" value="1" /> Daily
                                            </label>
                                            <br/>
                                            <label>
                                                <Field type="radio" name="taskFrequency" value="2" /> Weekly             
                                            </label>
                                       </div>
                                        <hr />
                                        <div className="form-group">
                                            <label htmlFor="dueBy">Due By :</label>
                                            <Field className="form-control" type="time" name="dueBy" />
                                        </div>
                                        <hr />
                                        {isWeekly && (
                                            <Field as='select' className="form-control shadow-none" name="weeklyRepetition">
                                                <option value="0">Sunday</option>
                                                <option value="1">Monday</option>
                                                <option value="2">Tuesday</option>
                                                <option value="3">Wednesday</option>
                                                <option value="4">Thursday</option>
                                                <option value="5">Friday</option>
                                                <option value="6">Saturday</option>
                                            </Field>
                                        )}
                                    </Card.Body>
                                    <Card.Footer className="clearfix">
                                        <Button type="submit" variant="outline-info" className='float-right'>Done</Button>
                                    </Card.Footer>
                                </Form>
                            </div>
                        )}
                    </Formik>
                </Card>
            </Modal.Body>
        </Modal>
    )
}

export default TaskFrequencyModal;