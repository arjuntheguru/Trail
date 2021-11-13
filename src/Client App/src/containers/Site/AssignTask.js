import React, { useEffect, useState, useRef } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { ListGroup, Modal, Row, Col, Button, Pagination, Spinner } from 'react-bootstrap';
import TaskFrequencyModal from './TaskFrequencyModal';

import { getUnallocatedTask, allocateTask } from "../../store/task-actions";
import { succeeded } from '../../utils/stateStatus';

import _ from 'lodash';

const AssignTask = ({ data, show, onClose }) => {

    const { status, taskInfo } = useSelector(state => state.task);

    const [pageNumber, setPageNumber] = useState(1);
    const [previousDisabled, setPreviousDisabled] = useState(true);
    const [nextDisabled, setnextDisabled] = useState(false);    

    const [showTaskFrequencyModal, setTaskFrequencyModal] = useState(false);
    const [taskAllocationData, setTaskAllocationData] = useState({});


    const dispatch = useDispatch();

    const taskFrequencyHandler = (task) => {
        setTaskAllocationData({
            SiteId: data,
            TaskId: task.id
        })

        setTaskFrequencyModal(true);
    };

    const onSubmitCallback = (frequencyData) => {

        const taskAllocation = {

            ...taskAllocationData,
            TaskFrequency: parseInt(frequencyData.taskFrequency),
            DueBy: frequencyData.dueBy,
            WeeklyRepetition: parseInt(frequencyData.weeklyRepetition)
        }

        dispatch(allocateTask(taskAllocation));

        setTaskFrequencyModal(false);
        onClose();
    };

    const taskFrequencyModalCloseHandler = () => {
        setTaskFrequencyModal(false);
    };

    useEffect(() => {
        console.log(data);
        dispatch(getUnallocatedTask(data));
    }, [dispatch, data])

    useEffect(() => {
        if(!_.isEmpty(taskInfo)){
            if (taskInfo.count <= 10) {
                setnextDisabled(true)
            }
            else {
                setnextDisabled(false);
            }
        }        

    }, [taskInfo, taskInfo.count]);


    const previousPageHandler = () => {
        dispatch(getUnallocatedTask(data, { pageNumber: pageNumber - 1, pageSize: 10 }))
        setnextDisabled(false)

        if (pageNumber - 1 === 1) {
            setPreviousDisabled(true);
        }
        setPageNumber(pageNumber => pageNumber - 1);
    };

    const nextPageHandler = () => {
        dispatch(getUnallocatedTask(data, { pageNumber: pageNumber + 1, pageSize: 10 }))
        setPreviousDisabled(false)

        if (pageNumber + 1 === Math.ceil(taskInfo.count / 10)) {
            setnextDisabled(true);
        }
        setPageNumber(pageNumber => pageNumber + 1);
    };


    return (
        <Modal show={show} onHide={onClose} centered >
            {showTaskFrequencyModal ? <TaskFrequencyModal show={showTaskFrequencyModal} onClose={taskFrequencyModalCloseHandler} callback={onSubmitCallback} /> : ''}
                                    <Modal.Header className="bg-light text-dark">
                                        Assign Task
                                        </Modal.Header>
            <Modal.Body className="">
                    {
                        status === succeeded ?
                            !_.isEmpty(taskInfo.data) ?
                                <>
                                     <ListGroup variant="flush">
                                        {taskInfo.data.map((task) => {
                                            return (
                                                <>
                                               
                                                    <ListGroup.Item
                                                        as="li"
                                                        className="pl-0 py-2 d-flex justify-content-between align-items-start"
                                                    >
                                                        <div className="ms-2 me-auto">
                                                        {task.name}
                                                        </div>
                                                        <Button variant="outline-primary" type="button" size="sm" onClick={() => taskFrequencyHandler(task)}>Assign</Button>
                                                    </ListGroup.Item>
                                                    
                                                </>
                                            )
                                        })}
                                        </ListGroup> 
                                        <Pagination className="justify-content-center">
                                            <Pagination.First disabled={previousDisabled} onClick={previousPageHandler} />
                                            <Pagination.Item disabled={true}>{pageNumber} of {Math.ceil(taskInfo.count / 10)} </Pagination.Item>
                                            <Pagination.Next disabled={nextDisabled} onClick={nextPageHandler}/>
                                        </Pagination>
                                </>
                                : 
                                    <div>
                                        <h4 className="text-muted text-center">Either all tasks are assigned or there are no tasks.</h4>
                                    </div>
                            : <Spinner animation="border spinner" />
                    }
            </Modal.Body>
        </Modal >
    )
};

export default AssignTask;