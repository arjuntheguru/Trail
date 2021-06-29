import React, { useEffect, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Link } from "react-router-dom";
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

import { getUser, deleteUser, filter } from "../../store/user-actions";
import { getSiteWithoutPagination } from "../../store/site-actions";
import { failed, idle, succeeded } from "../../utils/stateStatus";

import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
	faPencilAlt,
	faEllipsisV,
	faTrash,
	faTrashRestore,
	faSearch,
	faPlusCircle,
} from "@fortawesome/free-solid-svg-icons";

import AddUser from "./AddUser";
import EditUser from "./EditUser";
import DeleteUser from "./DeleteUser";

const ManageUser = () => {
	const dispatch = useDispatch();

	const { companyId } = useSelector((state) => state.auth.user);
	const { status, userInfo } = useSelector((state) => state.user);
	const {
		status: siteStatus,
		siteInfo,
		recentGETMethod,
	} = useSelector((state) => state.site);

	const [showAddModal, setShowAddModal] = useState(false);
	const [showEditModal, setShowEditModal] = useState(false);
	const [showDeleteModal, setShowDeleteModal] = useState(false);

	const [editData, setEditData] = useState({});
	const [deleteData, setDeleteData] = useState({});

	const addHandler = () => {
		setShowAddModal(true);
	};

	const editHandler = (data) => {
		setEditData(data);
		setShowEditModal(true);
	};

	const deleteHandler = (data) => {
		setDeleteData(data);
		setShowDeleteModal(true);
	};

	const restoreHandler = (data) => {
		var res = window.confirm(`Restore user ${data.firstName} ${data.lastName}`);
		console.log(res);
		if (res) {
			dispatch(deleteUser(data, true));
		}
	};

	const addModalCloseHandler = () => {
		setShowAddModal(false);
	};

	const editModalCloseHandler = () => {
		setShowEditModal(false);
	};

	const deleteModalCloseHandler = () => {
		setShowDeleteModal(false);
	};

	const searchHandler = (value) => {
		console.log(value);
		dispatch(filter(value.toLowerCase()));
	};

	useEffect(() => {
		if (
			siteStatus === idle ||
			(siteStatus === succeeded &&
				recentGETMethod !== "getSiteWithoutPagination")
		) {
			dispatch(getSiteWithoutPagination(companyId));
		}
	}, [dispatch, companyId, siteStatus, recentGETMethod]);

	useEffect(() => {
		if (status === idle || status === failed) {
			dispatch(getUser(companyId));
		}
	}, [dispatch, companyId, status]);

	return (
		<div>
			{status === succeeded && siteStatus === succeeded ? (
				<>
					<section className="site-breadcrumb bg-light">
						<Container className="text-center">
							<Row>
								<Col>
									<h5>Manage User</h5>
									<Breadcrumb className="justify-content-center">
										<Breadcrumb.Item>
											<Link to="/">Home</Link>
										</Breadcrumb.Item>
										<Breadcrumb.Item active>User</Breadcrumb.Item>
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
												<InputGroup.Text id="basic-addon1">
													<FontAwesomeIcon icon={faSearch} />{" "}
												</InputGroup.Text>
												<FormControl
													placeholder="Search"
													aria-label="Search"
													aria-describedby="basic-addon1"
													onChange={(e) => {
														searchHandler(e.target.value);
													}}
												/>
											</InputGroup>
											<Button
												type="button"
												variant="primary"
												className="float-right"
												onClick={() => {
													addHandler();
												}}
											>
												<FontAwesomeIcon icon={faPlusCircle} /> Add User
											</Button>
										</header>

										<>
											{showAddModal ? (
												<AddUser
													show={showAddModal}
													onClose={addModalCloseHandler}
													siteInfo={siteInfo}
													data={companyId}
												/>
											) : (
												""
											)}
											{showEditModal ? (
												<EditUser
													show={showEditModal}
													onClose={editModalCloseHandler}
													siteInfo={siteInfo}
													data={editData}
												/>
											) : (
												""
											)}
											{showDeleteModal ? (
												<DeleteUser
													show={showDeleteModal}
													onClose={deleteModalCloseHandler}
													data={deleteData}
												/>
											) : (
												""
											)}

											<Table striped bordered size="">
												<thead variant="dark" className="bg-dark text-light">
													<tr>
														<th>Name</th>
														<th>Email</th>
														<th>Username</th>
														<th>Status</th>
														<th>Role</th>
														<th>Access</th>
														<th></th>
													</tr>
												</thead>
												<tbody>
													{userInfo.length > 0 ? (
														userInfo.map((user) => {
															return (
																<tr
																	className=""
																	style={{
																		fontSize: "14px",
																	}}
																>
																	<td>{`${user.firstName} ${user.lastName}`}</td>
																	<td>{user.email}</td>
																	<td>{user.userName}</td>
																	<td>
																		{user.isActive ? (
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
																	<td>{user.role}</td>
																	<td className="text-muted">
																		{user.siteId == null
																			? "All Sites"
																			: user.siteId.map((siteId, i) => {
																					if (i < 2) {
																						return (
																							<span>
																								{i === 0 ? "" : ", "}
																								{
																									siteInfo.data.find(
																										(item) => item.id === siteId
																									).name
																								}
																							</span>
																						);
																					} else {
																						if (!user.siteId[i + 1]) {
																							return (
																								<span>, +{i - 1} more</span>
																							);
																						}
																					}
																			  })}{" "}
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
																						editHandler(user);
																					}}
																				>
																					<FontAwesomeIcon
																						icon={faPencilAlt}
																						className="mr-2"
																					/>{" "}
																					Edit
																				</Dropdown.Item>
																				{user.isActive ? (
																					<Dropdown.Item
																						onClick={() => {
																							deleteHandler(user);
																						}}
																					>
																						<FontAwesomeIcon
																							icon={faTrash}
																							className="mr-2"
																						/>{" "}
																						Delete
																					</Dropdown.Item>
																				) : (
																					<Dropdown.Item
																						onClick={() => {
																							restoreHandler(user);
																						}}
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
														<tr>
															<td colSpan={7} className="text-center">
																No Data
															</td>
														</tr>
													)}
												</tbody>
											</Table>
										</>
									</div>
								</Col>
							</Row>
						</Container>
					</section>
				</>
			) : (
				<Spinner animation="border spinner" />
			)}
		</div>
	);
};

export default ManageUser;
