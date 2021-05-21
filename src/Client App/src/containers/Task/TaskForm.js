import React, { useState } from 'react';
import { Formik, Form, FieldArray } from 'formik';
import { useDispatch } from "react-redux";
import { addTask, getTask, updateTask } from "../../store/task-actions";
import { InputField } from '../../components/InputField/InputField';
import { TextArea } from '../../components/TextArea/TextArea';
import * as Yup from 'yup';

import { Card, Button, Row, Col, Popover, Overlay, OverlayTrigger } from 'react-bootstrap';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faTrash } from '@fortawesome/free-solid-svg-icons'


const TaskForm = ({ isEdit = false, data = {}, onClose, skip }) => {

    const initialValues = {
        id: '',
        name: '',
        description: '',
        taskItemInfo: [
            { name: '' }
        ]
    };

    const initialValuesEdited = {
        id: data.id,
        name: data.name,
        description: data.description,
        taskItemInfo: data.taskItemInfo
    }

    const [showPopover, setShowPopover] = useState(false)

    const popover = (
        <Popover id="popover-basic">            
            <Popover.Content>
                Atleast <strong>one</strong> item is required.
          </Popover.Content>
        </Popover>
    );


    const taskInfoValidate = Yup.object().shape({
        name: Yup.string()
            .max(15, 'Must be 15 characters or less')
            .required('Name is required'),
        description: Yup.string()
            .required('Description is required'),
        taskItemInfo: Yup.array().of(
            Yup.object().shape({
                name: Yup.string().required('Task Item is required')
            })
        )
    });

    const dispatch = useDispatch();

    const addTaskItemHandler = (values, setValues, setTouched) => {        
        const taskItemInfo = [...values.taskItemInfo]
        setTouched({})
        taskItemInfo.push({ name: '' });
        setValues({ ...values, taskItemInfo });
        //setShowPopover(false)
    }

    const deleteTaskHandler = (e, values, setValues, setTouched, name) => {

        const taskItemInfo = [...values.taskItemInfo]
        const index = parseInt(e.currentTarget.dataset.index)           

        if (taskItemInfo.length > 1) {
            taskItemInfo.splice(index, 1)
            setValues({ ...values, taskItemInfo });                
            setShowPopover(false)
            setTouched({})
        }
        else {
            setShowPopover(true)
        }

    }

    return (
        <Formik
            initialValues={!isEdit ? initialValues : initialValuesEdited}
            validationSchema={taskInfoValidate}
            onSubmit={values => {

                let taskInfo = {

                    Id: values.id,
                    Name: values.name,
                    Description: values.description,
                    TaskItemInfo : values.taskItemInfo

                };

                if (!isEdit) {

                    const callback = () => {
                        const pageNumberValue = (skip / 10) + 1;
                        dispatch(getTask({ pageNumber: pageNumberValue, pageSize: 10 }));
                    };
                    console.log(values);
                    dispatch(addTask(taskInfo, callback));
                    onClose();

                }
                else {
                    console.log(values)
                    dispatch(updateTask(taskInfo))
                    onClose();
                }
            }}
        >
            {({ values, setValues, setTouched }) => (
                <div>
                    <Card className="bg-transparent">
                        <Card.Header className='bg-dark text-light text-center'>{!isEdit ? "Add Task" : "Edit Task"}</Card.Header>
                        <Form>
                            <Card.Body className='card-body'>
                                <InputField label="Name" name="name" type="text" />
                                <TextArea label="Description" name="description" />
                            </Card.Body>
                            <Card className='mt-1 bg-transparent'>
                                <Card.Header className='bg-dark text-light text-center'>Task Item </Card.Header>
                                <Card.Body>
                                    <FieldArray name="taskItemInfo">
                                        {() => (values.taskItemInfo.map((taskItem, i) => {
                                            return (
                                                <Row className="my-1">
                                                    <Col md={1} className="mt-1">
                                                        <span className="">{i + 1}.</span>
                                                    </Col>
                                                    <Col md={10}>
                                                        <InputField isLabel={false} name={`taskItemInfo.${i}.name`} type="text" />
                                                    </Col>
                                                    <Col md={1}>
                                                        <OverlayTrigger overlay={popover}  show={showPopover} trigger="click" onToggle={() => {setShowPopover(!showPopover)}} rootClose>
                                                            <Button variant="danger" size="sm" data-index={i} onClick={(e) => deleteTaskHandler(e, values, setValues, setTouched)} type="button">
                                                                <FontAwesomeIcon icon={faTrash} className="text-light" />
                                                            </Button>
                                                        </OverlayTrigger>

                                                    </Col>
                                                </Row>
                                            )
                                        }))}
                                    </FieldArray>
                                    <div className="text-center">
                                        <Button type="button" variant="outline-success" className='w-25 my-2' onClick={() => { addTaskItemHandler(values, setValues, setTouched) }}>
                                            Add Item
                                    </Button>
                                    </div>

                                </Card.Body>
                            </Card>
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


export default TaskForm;