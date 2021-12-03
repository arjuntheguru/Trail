import React, { useEffect, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
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
import { getTask, getTaskAdmin } from "../../store/task-actions";
import { failed, idle, succeeded } from "../../utils/stateStatus";
import TablePagination from "../../components/Pagination/Pagination";

import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
	faPencilAlt,
	faEllipsisV,
	faTrash,
	faTrashRestore,
	faSearch,
	faEdit,
	faPlusCircle,
} from "@fortawesome/free-solid-svg-icons";

import "../../App.scss";
import EditTask from "./EditTask";
// import DeleteTask from './DeleteTask';
import AddTask from "./AddTask";

import _ from "lodash";
import { returnLoggedInUser } from "../../store/auth-actions";

const Task = () => {
	const [currentPageNumber, setCurrentPageNumber] = useState(1);

	const { status, taskInfo } = useSelector((state) => state.task);
	const [skip, setSkip] = useState(0);

	const [showEditModal, setShowEditModal] = useState(false);
	const [showAddModal, setShowAddModal] = useState(false);

	const [editData, setEditData] = useState({});

	const dispatch = useDispatch();

	const user = dispatch(returnLoggedInUser());

	const addHandler = () => {
		setShowAddModal(true);
	};

	const editHandler = (data) => {
		console.log(data);
		setEditData(data);
		setShowEditModal(true);
	};

	const addModalCloseHandler = () => {
		setShowAddModal(false);
	};

	const editModalCloseHandler = () => {
		setShowEditModal(false);
	};

	const skipChangeHandler = (skipValue) => {
		const pageNumberValue = skipValue / 10 + 1;
		dispatch(getTask({ pageNumber: pageNumberValue, pageSize: 10 }));
		setSkip(skipValue);
	};

	useEffect(() => {
		if (user.role === "SuperAdmin") {
			dispatch(getTask());
		} else {
			dispatch(getTaskAdmin());
		}
	}, [dispatch, user.role]);

	return (
		<>
			{status === succeeded ? (
				<>
					<section className="site-breadcrumb">
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
													<Button variant="primary">
														<FontAwesomeIcon icon={faSearch} />{" "}
													</Button>
												</InputGroup.Append>
											</InputGroup>
											<Button
												type="button"
												variant="primary"
												className="float-right"
												onClick={() => {
													addHandler();
												}}
											>
												<FontAwesomeIcon icon={faPlusCircle} /> Add Task
											</Button>
										</header>
										{showAddModal ? (
											<AddTask
												show={showAddModal}
												onClose={addModalCloseHandler}
												pageNumber={currentPageNumber}
											/>
										) : (
											""
										)}
										{showEditModal ? (
											<EditTask
												show={showEditModal}
												onClose={editModalCloseHandler}
												data={editData}
											/>
										) : (
											""
										)}
										<Table striped size="">
										<thead className="bg-dark text-light">
												<tr>
													<th>Name</th>
													<th>Description</th>
													<th>Task Type</th>
													<th>Status</th>
													<th></th>
												</tr>
											</thead>
											<tbody>
												{taskInfo.data.length > 0 ? (
													taskInfo.data.map((task) => {
														return (
															<tr>
																<td>{task.name}</td>
																<td>{task.description}</td>
																<td>
																	{task.taskType === 0
																		? "Checklist"
																		: "Record Log"}
																</td>
																<td>
																	{task.status ? (
																		<Badge
																			pill
																			variant="success"
																			className="px-3 py-2 mx-1"
																		>
																			Active
																		</Badge>
																	) : (
																		<Badge
																			pill
																			variant="danger"
																			className="px-3 py-2 mx-1"
																		>
																			Inactive
																		</Badge>
																	)}
																</td>
																<td>
																	<Dropdown alignRight>
																		<Dropdown.Toggle
																			variant="link"
																			size="sm"
																			id="dropdown-basic"
																			className="caret-clear btn-circle"
																		>
																			<FontAwesomeIcon icon={faEllipsisV} />
																		</Dropdown.Toggle>

																		<Dropdown.Menu className="border-0 shadow-sm">
																			<Dropdown.Item
																				onClick={() => {
																					editHandler(task);
																				}}
																			>
																				<FontAwesomeIcon
																					icon={faPencilAlt}
																					className="mr-2"
																				/>{" "}
																				Edit
																			</Dropdown.Item>
																			{task.status ? (
																				<Dropdown.Item
																				// onClick={() => {
																				// 	deleteHandler(user);
																				// }}
																				>
																					<FontAwesomeIcon
																						icon={faTrash}
																						className="mr-2"
																					/>{" "}
																					Delete
																				</Dropdown.Item>
																			) : (
																				<Dropdown.Item
																				// onClick={() => {
																				// 	restoreHandler(user);
																				// }}
																				>
																					<FontAwesomeIcon
																						icon={faTrashRestore}
																						className="mr-2"
																					/>{" "}
																					Restore
																				</Dropdown.Item>
																			)}
																		</Dropdown.Menu>
																	</Dropdown>
																</td>
															</tr>
														);
													})
												) : (
													<tr className="bg-transparent">
														<td colSpan="100%">
															<div className="text-center h-100 p-5">
																<FontAwesomeIcon icon={faSearch} size="3x" />
																<h6>Result Not Found</h6>
																<p className="text-muted">
																	Please change keyword or modify filters
																</p>
															</div>
														</td>
													</tr>
												)}
											</tbody>
											<tfoot>
												<tr>
													<td colSpan="100%" className="p-3">
														<TablePagination
															currentPageNumber={currentPageNumber}
															pageNumberChange={setCurrentPageNumber}
															totalRecords={taskInfo.count}
															callback={
																user.role === "SuperAdmin"
																	? getTask
																	: getTaskAdmin
															}
														/>
													</td>
												</tr>
											</tfoot>
										</Table>
									</div>
								</Col>
							</Row>
						</Container>
					</section>
				</>
			) : (
				<> </>
			)}
		</>
	);
};

export default Task;
