import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Col, Row, Spinner, Button } from 'react-bootstrap';
import { getTask } from '../../store/task-actions';

import ReactDataGrid from '@inovua/reactdatagrid-community';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faEdit } from '@fortawesome/free-solid-svg-icons'

import '../../App.css';
import EditTask from './EditTask';
// import DeleteTask from './DeleteTask';
import AddTask from './AddTask';

import _ from 'lodash';

const gridStyle = { minHeight: 485, marginTop: 10 };


const Task = () => {

    const columns = [

        { name: 'name', defaultFlex: 1, header: 'Name' },  
        { name: 'description', defaultFlex: 2, header: 'Description' },      
        {
            header: 'Edit',
            name: 'id',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => editHandler(data)}>
                            <FontAwesomeIcon icon={faEdit} className="text-light"/>
                        </Button>
                    </div>
                )
            }
        } 
        
    ];

    const { isLoading, taskInfo } = useSelector(state => state.task);
    const [skip, setSkip] = useState(0);

    const [showEditModal, setShowEditModal] = useState(false);    
    const [showAddModal, setShowAddModal] = useState(false);

    const [editData, setEditData] = useState({});    

    const dispatch = useDispatch();

    const addHandler = () => {        
        
        setShowAddModal(true);
    }

    const editHandler = (data) => {
        console.log(data)        
        setEditData(data);
        setShowEditModal(true);
    }
    

    const addModalCloseHandler = () => {
        setShowAddModal(false);
    }

    const editModalCloseHandler = () => {
        setShowEditModal(false);
    }   

    const skipChangeHandler = (skipValue) => {

        const pageNumberValue = (skipValue / 10) + 1;
        dispatch(getTask({ pageNumber: pageNumberValue, pageSize: 10 }))
        setSkip(skipValue);
    }

    useEffect(() => {
        
        dispatch(getTask())

    }, [dispatch])

    return (
        <div>
            <Row className="mt-2">
                <Col md={10}>
                    <h1 className="display-4"> Task </h1>
                </Col>
                <Col md={2}>
                    <Button type="button" variant="outline-dark" className='mt-4 float-right'  onClick={() => {addHandler()}}>Add Task</Button>
                </Col>
            </Row>
            <hr />
            <AddTask show={showAddModal} onClose={addModalCloseHandler} skip={skip}/>
            {showEditModal ? <EditTask show={showEditModal} onClose={editModalCloseHandler} data={editData} /> : ''}            
            <Row>
                {
                    !isLoading ?
                        !_.isEmpty(taskInfo)
                            ? <ReactDataGrid
                                idProperty="id"
                                style={gridStyle}
                                columns={columns}
                                pagination
                                skip={skip}
                                sortable={false}
                                showColumnMenuTool={false}
                                onSkipChange={skipChangeHandler}
                                pageSizes={false}
                                theme="default-light"
                                dataSource={new Promise(function (resolve, reject) {
                                    resolve(taskInfo)
                                })}
                                defaultLimit={10}
                            />

                            : <Spinner animation="border spinner" />
                        : <Spinner animation="border spinner" />
                }
            </Row>
        </div>
    )

   

};

export default Task;