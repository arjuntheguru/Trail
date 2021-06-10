import React, { useState, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { getSiteById } from '../../store/site-actions';

import { Col, Row, Spinner, Button, Accordion } from 'react-bootstrap';
import DatePicker from "react-datepicker";
import { getCalendar } from '../../store/calendar-actions';
import TaskList from '../../components/TaskList/TaskList';
import AssignTask from './AssignTask';

import _ from 'lodash';
import { succeeded } from '../../utils/stateStatus';

const ManageTask = ({ match }) => {

    const [startDate, setStartDate] = useState(new Date());
    const [showAssignTaskModal, setShowAssignTaskModal] = useState(false);

    const { status: calendarStatus, calendarInfo } = useSelector(state => state.calendar);
    const { status: siteStatus, siteById } = useSelector(state => state.site)

    const assignTaskHandler = () => {
        setShowAssignTaskModal(true);
    }

    const assignTaskModalCloseHandler = () => {
        setShowAssignTaskModal(false);
    }

    const dispatch = useDispatch();

    useEffect(() => {

        dispatch(getSiteById(match.params.id))

    }, [dispatch, match.params.id])

    useEffect(() => {

        dispatch(getCalendar(match.params.id, startDate.toISOString().split('T')[0]))

    }, [dispatch, match.params.id, startDate])

    const orderByDueDate = () => {
        let orderedData = _.orderBy(calendarInfo.data.taskList, ['dueBy'], ['asc'])
        return orderedData;
    }


    return (
        <>
            {
                siteStatus === succeeded ?
                    (!_.isEmpty(siteById) && siteById.id === match.params.id) ? (
                        <div>
                            {showAssignTaskModal ? <AssignTask show={showAssignTaskModal} onClose={assignTaskModalCloseHandler} data={match.params.id} /> : ''}
                            <Row className="mt-2">
                                <Col md={10}>
                                    <h1 className="display-4">{siteById.name}</h1>
                                </Col>
                                <Col md={2}>
                                    <Button type="button" variant="outline-dark" className='mt-4 float-right' onClick={assignTaskHandler}>Assign Task</Button>
                                </Col>
                            </Row>
                            <hr />
                            <Row>
                                <Col md={2}>
                                    <DatePicker
                                        selected={startDate}
                                        onChange={date => setStartDate(date)}
                                        maxDate={new Date()}
                                        todayButton="Today"
                                        className="form-control"
                                    />
                                </Col>
                            </Row>
                            <hr />
                            {
                                calendarStatus === succeeded ?
                                    !_.isEmpty(calendarInfo.data) ?
                                        <Accordion defaultActiveKey="0">

                                            {orderByDueDate().map((taskListItem, i) => {
                                                return (
                                                    <>
                                                        <TaskList data={taskListItem} index={i.toString()} calendarId={calendarInfo.data.id} />
                                                        <br />
                                                    </>
                                                )
                                            })}
                                        </Accordion>
                                        : <h4 className="text-muted center">No Tasks</h4>
                                    : <Spinner animation="border spinner" />

                            }

                        </div>
                    ) : <Spinner animation="border spinner" /> 
                    
                    : <Spinner animation="border spinner" />
                }
        </>
    )
}

export default ManageTask