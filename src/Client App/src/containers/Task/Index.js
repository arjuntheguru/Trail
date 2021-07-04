import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import {
	Container,
	Col,
	Row,
	Spinner,
	Button,
	Table,
	Badge,
	Breadcrumb,
	Dropdown,
	InputGroup,
	FormControl,
} from "react-bootstrap";
import { getTask, getTaskFromCompanyId } from '../../store/task-actions';

import ReactDataGrid from '@inovua/reactdatagrid-community';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import {
	faPencilAlt,
	faEllipsisV,
	faTrash,
	faTrashRestore,
	faSearch,
    faEdit,
	faPlusCircle,
} from "@fortawesome/free-solid-svg-icons";

import '../../App.scss';
import EditTask from './EditTask';
// import DeleteTask from './DeleteTask';
import AddTask from './AddTask';

import _ from 'lodash';
import { returnLoggedInUser } from '../../store/auth-actions';

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
                            <FontAwesomeIcon icon={faEdit} className="text-light" />
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

    const user = dispatch(returnLoggedInUser())

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

        if (user.role === "SuperAdmin") {
            dispatch(getTask())
        }
        else {
            dispatch(getTaskFromCompanyId(user.companyId))
        }

    }, [dispatch, user.role, user.companyId])

    return (
        <>
                    <section className="site-breadcrumb bg-light">
						<Container className="text-center">
							<Row>
								<Col>
									<h5 className="mb-0">Manage Task</h5>
									<Breadcrumb className="justify-content-center">
										<Breadcrumb.Item>Home</Breadcrumb.Item>
										<Breadcrumb.Item active>Task</Breadcrumb.Item>
									</Breadcrumb>
								</Col>
							</Row>
						</Container>
					</section>
                    <section>
						<Container>
							<Row>
								<Col>
									<div className="table-container bg-white">
										<header className="table-cta p-2 clearfix">
											<InputGroup className="w-25 float-left">
												<FormControl
													placeholder="Search"
													aria-label="Search"
													aria-describedby="basic-addon1"
												/>
                                                <InputGroup.Append>
                                                    <Button variant="primary"><FontAwesomeIcon icon={faSearch} />{" "}</Button>
                                                </InputGroup.Append>
											</InputGroup>
											<Button
												type="button"
												variant="primary"
												className="float-right"
												onClick={() => { addHandler() }}
											>
												<FontAwesomeIcon icon={faPlusCircle} /> Add User
											</Button>
										</header>
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
                                    </div>
                                </Col>
                            </Row>
                        </Container>
                    </section>
            <AddTask show={showAddModal} onClose={addModalCloseHandler} skip={skip} />
            {showEditModal ? <EditTask show={showEditModal} onClose={editModalCloseHandler} data={editData} /> : ''}
           
        </>
    )



};

export default Task;