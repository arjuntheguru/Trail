import { Formik, Form, Field, ErrorMessage } from 'formik';
import React from 'react';
import { useDispatch } from 'react-redux';
import { Accordion, Card, Button, Badge, Row, Col } from 'react-bootstrap';
import TaskItem from '../TaskItem/TaskItem';
import { updateTaskListStatus, insertComment } from '../../store/calendar-actions';
import * as Yup from 'yup';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faBan } from '@fortawesome/free-solid-svg-icons'

import { getLocalDate } from '../../utils/dateUtility';


const TaskList = ({ data, index, calendarId }) => {

    const dispatch = useDispatch();

    return (
        <Card className="mb-2 rounded-sm">
            <Accordion.Toggle as={Card.Header} eventKey={index} className='bg-light rounded-sm'>
                <Row>
                    <Col md={7}>
                        {data.name}
                    </Col>
                    <Col md={5} className="text-right px-0">
                        {
                            data.status === 0 ?
                                <Badge pill variant="danger" className="px-3 py-2 mx-1">
                                    Incomplete
                                </Badge> :
                                data.status === 1 ?
                                    <Badge pill variant="primary" className="px-3 py-2 mx-1">
                                        Completed
                                </Badge> :
                                    <Badge pill variant="warning" className="px-3 py-2 mx-1">
                                        Late Completion
                                </Badge>
                        }

                        <Badge pill variant="light" className="px-3 py-2 mx-1">
                            Due by {new Date(data.dueBy).toLocaleTimeString()}
                        </Badge>
                    </Col>
                </Row>


            </Accordion.Toggle>
            <Accordion.Collapse eventKey={index}>
                <>
                    <Card.Body>
                        {data.description}
                        <hr />
                        {data.taskItems.map((taskItem, i) =>
                            <TaskItem
                                taskItem={taskItem}
                                parentId={data.id}
                                calendarId={calendarId}
                                locked={data.isApproved}
                            />)}
                    </Card.Body>
                    <Card.Footer className="">
                        
                            <div style={{maxHeight:"180px"}} className="overflow-auto">
                                {data.comments.length > 0 &&
                                    data.comments.map((comment, i) => {
                                        return (
                                            <div className="clearfix my-2">
                                                <Badge pill variant={i % 2 === 0 ? "info" : "secondary"} className="px-3 py-2 mx-1">
                                                    {comment.description}
                                                </Badge>
                                                <small className="float-right text-muted">By {comment.createdBy} at {getLocalDate(comment.createdDate)} </small>
                                            </div>
                                        )
                                    })}
                            </div>
                        <Row>
                                <Col >
                                {!data.isApproved &&
                            <Formik
                                initialValues={{ approvedBy: '' }}
                                validationSchema={Yup.object({
                                    approvedBy: Yup.string()
                                        .required("Field cannot be empty")
                                })}
                                onSubmit={values => {

                                    var item = {

                                        CalendarId: calendarId,
                                        TaskListId: data.id,
                                        ApprovedBy: values.approvedBy
                                    }

                                    dispatch(updateTaskListStatus(item));
                                }}
                            >
                                {formik => (

                                    <Form>
                                        <div class="form-group">
                                                 <label htmlFor="approvedBy">Manager Sign-off</label>
                                                <Field as="textarea" row="3" name="approvedBy" className="form-control mb-3" />
                                                
                                                <Button variant="primary" type="submit">Submit</Button>
                                                
                                            <ErrorMessage component="div" name="approvedBy" className="error" />
                                        </div>
                                    </Form>
                                )}
                            </Formik>
                        }
                        {data.isApproved &&
                            <div className="text-center">
                                <Badge pill variant="dark" className="px-3 py-2 mx-1">
                                    Approved by {data.approvedBy} at {getLocalDate(data.approvedDate)}
                                </Badge>
                            </div>
                        }
                                </Col>
                                <Col >
                                <Formik
                            initialValues={{ comment: '' }}
                            validationSchema={Yup.object({
                                comment: Yup.string()
                                    .required("Comment cannot be empty")
                            })}
                            onSubmit={(values, { resetForm }) => {

                                var item = {

                                    CalendarId: calendarId,
                                    TaskListId: data.id,
                                    Description: values.comment
                                }

                                dispatch(insertComment(item));
                                resetForm();
                            }}
                        >
                            {formik => (
                                <Form>
                                    <div class="form-group">
                                            <label htmlFor="comment">Comment</label>
                                            <Field as="textarea" name="comment" row="3" className="form-control mb-3"/>
                                            
                                                <Button variant="primary" type="submit">Submit</Button>
                                            
                                        <ErrorMessage component="div" name="comment" className="error" />
                                    </div>
                                </Form>
                            )}
                        </Formik>
                        {data.isApproved &&
                            <div className="text-center">
                                <FontAwesomeIcon icon={faBan} className="text-dark" size="sm" /><small>&nbsp;&nbsp;Task is Locked </small>
                            </div>
                        }
                                 </Col>
                        </Row>
                        

                        
                    </Card.Footer>
                </>
            </Accordion.Collapse>
        </Card>
    )

};

export default TaskList;