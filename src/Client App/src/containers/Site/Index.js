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

import { getSiteAdmin, getSiteManager } from "../../store/site-actions";

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

import TablePagination from "../../components/Pagination/Pagination";

const Site = () => {
	const dispatch = useDispatch();

	const user = dispatch(returnLoggedInUser());

	const { status, siteInfo, recentGETMethod } = useSelector(
		(state) => state.site
	);
	const { companyId } = useSelector((state) => state.auth.user);

	const [showEditModal, setShowEditModal] = useState(false);
	const [showAddModal, setShowAddModal] = useState(false);
	const [showDeleteModal, setShowDeleteModal] = useState(false);

	const [editData, setEditData] = useState({});
	const [deleteData, setDeleteData] = useState({});

	const [currentPageNumber, setCurrentPageNumber] = useState(1);

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

	useEffect(() => {
		if (
			status === idle ||
			(status === succeeded && recentGETMethod !== "getSite")
		) {
			if (user.role === "Admin") {
				dispatch(getSiteAdmin());
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
									<h5 className="mb-0">Manage Site</h5>
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
													pageNumber={currentPageNumber}
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

											<Table striped size="">
												<thead className="bg-light text-dark">
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
																
																
																<tr>
																	<td>
																		<Link to={`/site/${site.id}`}>
																			{site.name}
																		</Link>
																	</td>
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
														
															<tr className="bg-transparent">
																<td colSpan="100%">
																	<div className="text-center h-100 p-5">
																		<FontAwesomeIcon icon={faSearch} size="3x" />
																		<h6>Result Not Found</h6>
																		<p className="text-muted">Please change keyword or modify filters</p>
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
																	totalRecords={siteInfo.count}
																	callback={getSiteAdmin}
																/>
															</td>
														</tr>
													</tfoot>
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
