import React, { useState, useEffect } from 'react';
import { useDispatch } from 'react-redux';
import { Row, Col, Form, FormCheck, Spinner } from 'react-bootstrap';
import { updateTaskItemStatus } from '../../store/calendar-actions';
import { getLocalDate } from '../../utils/dateUtility';
import { useSelector } from 'react-redux';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';

const TaskItem = ({ taskItem, parentId, calendarId, locked }) => {

    const [checked, setChecked] = useState(false);

    const [minorLoad, setMinorLoad] = useState(false);

    const dispatch = useDispatch();

    const taskStatusOnchangeHandler = (e) => {

        const item = {
            CalendarId: calendarId,
            TaskListId: parentId,
            TaskItemId: taskItem.id,
            TaskStatus: e.target.checked ? 1 : 0
        }

        // setChecked(state => !state);
        setMinorLoad(true);
        setTimeout(() => {
            setMinorLoad(false);
        }, 3000);
        dispatch(updateTaskItemStatus(item));
    };

    // const getLabel = () => {
    //     return !minorLoad? taskItem.name
    // }

    useEffect(() => {
        setChecked(taskItem.status === 1 ? true : false);
        setMinorLoad(false)
    }, [taskItem.status]);

    return (
        <Form>
            <Row className="my-2">
                <Col md={12}>
                    <Form.Check>
                        <FormCheck.Input
                            id={`taskStatus${taskItem.id}`}
                            checked={checked}
                            disabled={locked}
                            onChange={taskStatusOnchangeHandler}
                            type="checkbox"></FormCheck.Input>
                        <FormCheck.Label htmlFor={`taskStatus${taskItem.id}`}>
                            {taskItem.name}
                            {minorLoad && <Spinner size="sm" animation="border" />}
                        </FormCheck.Label>
                    </Form.Check>
                    {checked ? <small className="text-success m-0">Completed at {getLocalDate(taskItem.completedTime)} by {taskItem.completedBy} </small> : ''}
                </Col>
            </Row>
        </Form>
    );
};

export default TaskItem;
