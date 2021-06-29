import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
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

import { getSite, getSiteManager } from "../../store/site-actions";

import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
	faPencilAlt,
	faEllipsisV,
	faTrash,
	faTrashRestore,
	faSearch,
	faPlusCircle,
} from "@fortawesome/free-solid-svg-icons";

import "../../App.scss";
import EditSite from "./EditSite";
import AddSite from "./AddSite";
import DeleteSite from "./DeleteSite";

import _ from "lodash";
import { idle, succeeded } from "../../utils/stateStatus";
import { returnLoggedInUser } from "../../store/auth-actions";

const Site = () => {
	const dispatch = useDispatch();

	const user = dispatch(returnLoggedInUser());

	const { status, siteInfo, recentGETMethod } = useSelector(
		(state) => state.site
	);
	const { companyId } = useSelector((state) => state.auth.user);
	const [skip, setSkip] = useState(0);

	const [showEditModal, setShowEditModal] = useState(false);
	const [showAddModal, setShowAddModal] = useState(false);
	const [showDeleteModal, setShowDeleteModal] = useState(false);

	const [editData, setEditData] = useState({});
	const [deleteData, setDeleteData] = useState({});

	const addHandler = () => {
		setShowAddModal(true);
	};

	const editHandler = (data) => {
		console.log(data);
		setEditData(data);
		setShowEditModal(true);
	};

	const deleteHandler = (data) => {
		setDeleteData(data);
		setShowDeleteModal(true);
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

	const skipChangeHandler = (skipValue) => {
		const pageNumberValue = skipValue / 10 + 1;
		user.role === "Admin"
			? dispatch(
					getSite(companyId, { pageNumber: pageNumberValue, pageSize: 10 })
			  )
			: dispatch(getSiteManager({ pageNumber: pageNumberValue, pageSize: 10 }));
		setSkip(skipValue);
	};

	useEffect(() => {
		if (
			status === idle ||
			(status === succeeded && recentGETMethod !== "getSite")
		) {
			if (user.role === "Admin") {
				dispatch(getSite(companyId));
			} else {
				dispatch(getSiteManager());
			}
		}
	}, [dispatch, companyId, status, recentGETMethod, user.role]);

	return (
		<div>
			{status === succeeded ? (
				<>
					<section className="site-breadcrumb bg-light">
						<Container className="text-center">
							<Row>
								<Col>
									<h5>Manage Site</h5>
									<Breadcrumb className="justify-content-center">
										<Breadcrumb.Item>
											<Link to="/">Home</Link>
										</Breadcrumb.Item>
										<Breadcrumb.Item active>Site</Breadcrumb.Item>
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
												<FontAwesomeIcon icon={faPlusCircle} /> Add Site
											</Button>
										</header>
										<>
											{showAddModal ? (
												<AddSite
													show={showAddModal}
													onClose={addModalCloseHandler}
													siteInfo={siteInfo}
													data={companyId}
												/>
											) : (
												""
											)}
											{showEditModal ? (
												<EditSite
													show={showEditModal}
													onClose={editModalCloseHandler}
													siteInfo={siteInfo}
													data={editData}
												/>
											) : (
												""
											)}
											{showDeleteModal ? (
												<DeleteSite
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
														<th>Score Value</th>
														<th>Last Seen</th>
														<th>Status</th>
														<th></th>
													</tr>
												</thead>
												<tbody>
													{siteInfo.data.length > 0 ? (
														siteInfo.data.map((site) => {
															return (
																<tr
																	className=""
																	style={{
																		fontSize: "14px",
																	}}
																>
																	<td>{site.name}</td>
																	<td>{site.scoreValue}</td>
																	<td>{site.lastSeen.split("T")[0]}</td>
																	<td>
																		{site.isActive ? (
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
																						editHandler(site);
																					}}
																				>
																					<FontAwesomeIcon
																						icon={faPencilAlt}
																						className="mr-2"
																					/>{" "}
																					Edit
																				</Dropdown.Item>
																				{site.isActive ? (
																					<Dropdown.Item
																						onClick={() => {
																							deleteHandler(site);
																						}}
																					>
																						<FontAwesomeIcon
																							icon={faTrash}
																							className="mr-2"
																						/>{" "}
																						Delete
																					</Dropdown.Item>
																				) : (
																					""
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

export default Site;
