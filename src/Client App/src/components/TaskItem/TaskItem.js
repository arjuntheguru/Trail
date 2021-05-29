import React, { useState } from 'react';
import { useDispatch } from 'react-redux';
import { Row, Col, Form } from 'react-bootstrap';
import { updateTaskItemStatus } from '../../store/calendar-actions';
import { getLocalDate } from '../../utils/dateUtility';

const TaskItem = ({ taskItem, parentId, calendarId, locked }) => {

    const [checked, setChecked] = useState(taskItem.status === 1 ? true : false);

    const dispatch = useDispatch();

    const taskStatusOnchangeHandler = (e) => {

        const item = {
            CalendarId: calendarId,
            TaskListId: parentId,
            TaskItemId: taskItem.id,
            TaskStatus: e.target.checked ? 1 : 0
        }

        setChecked(state => !state);

        dispatch(updateTaskItemStatus(item));
    };    

    return (
        <Form>
            <Row className="my-2">
                <Col md={12}>
                    <Form.Check
                        type="checkbox"
                        id={`taskStatus${taskItem.id}`}
                        label={taskItem.name}
                        checked={checked}
                        disabled={locked}
                        onChange={taskStatusOnchangeHandler}
                    />
                    {checked ? <small className="text-success m-0">Completed at {getLocalDate(taskItem.completedTime)} by {taskItem.completedBy} </small> : ''}
                </Col>
            </Row>
        </Form>
    );
};

export default TaskItem;
