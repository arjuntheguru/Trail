import React, { useEffect, useState } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { Card, Modal, Row, Col, Button, Pagination } from 'react-bootstrap';
import TaskFrequencyModal from './TaskFrequencyModal';

import { getUnallocatedTask, allocateTask } from "../../store/task-actions";

const AssignTask = ({ data, show, onClose }) => {

    const { isLoading, taskInfo } = useSelector(state => state.task);
   
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
        <Modal show={show} onHide={onClose} centered size="lg" >
            <Modal.Body className="">
                <Card className="bgGradient">
                    {
                        !isLoading ?
                            Object.keys(taskInfo).length !== 0 ?
                                Object.keys(taskInfo.data).length !== 0 ?
                                    <>
                                        {showTaskFrequencyModal ? <TaskFrequencyModal show={showTaskFrequencyModal} onClose={taskFrequencyModalCloseHandler} callback={onSubmitCallback} /> : ''}
                                        <Card.Header className="bg-dark text-light">
                                            Assign Task
                                        </Card.Header>
                                        <Card.Body>
                                            {taskInfo.data.map((task) => {
                                                return (
                                                    <>
                                                        <Row className="broder border-bottom my-2">
                                                            <Col md={10}>
                                                                {task.name}
                                                            </Col>
                                                            <Col md={2} className="mb-3">
                                                                <Button variant="outline-success" type="button" size="sm" onClick={() => taskFrequencyHandler(task)}>Assign</Button>
                                                            </Col>
                                                        </Row>
                                                    </>
                                                )
                                            })}
                                        </Card.Body>
                                        <Card.Footer>
                                            <Pagination className="justify-content-center">
                                                <Pagination.Item disabled={previousDisabled} onClick={previousPageHandler}>Previous</Pagination.Item>
                                                <Pagination.Item disabled={true}>{pageNumber} of {Math.ceil(taskInfo.count / 10)} </Pagination.Item>
                                                <Pagination.Item disabled={nextDisabled} onClick={nextPageHandler}>Next</Pagination.Item>
                                            </Pagination>
                                        </Card.Footer>
                                    </>
                                    : <p>No records found</p>
                                : ''
                            : ''
                    }
                </Card>
            </Modal.Body>
        </Modal >
    )
};

export default AssignTask;