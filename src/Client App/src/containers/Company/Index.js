import React, { useEffect, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import {
	Container,
	Col,
	Row,
	Button,
	Table,
	Badge,
	Breadcrumb,
	Dropdown,
	InputGroup,
	FormControl,
} from "react-bootstrap";
import { getCompany } from "../../store/company-actions";

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
import EditCompany from "./EditCompany";
import DeleteCompany from "./DeleteCompany";
import AddCompany from "./AddCompany";
import ManageAdmin from "./ManageAdmin";

import TablePagination from "../../components/Pagination/Pagination";

import { idle, succeeded } from "../../utils/stateStatus";

const Company = () => {
	const dispatch = useDispatch();

	const [currentPageNumber, setCurrentPageNumber] = useState(1);

	const { status, companyInfo } = useSelector((state) => state.company);

	const [showEditModal, setShowEditModal] = useState(false);
	const [searchString, setSearchString] = useState("");
	const [showDeleteModal, setShowDeleteModal] = useState(false);
	const [showAddModal, setShowAddModal] = useState(false);
	const [showAdminModal, setShowAdminModal] = useState(false);

	const [editData, setEditData] = useState({});
	const [deleteData, setDeleteData] = useState({});
	const [adminData, setAdminData] = useState({});

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

	const adminHandler = (data) => {
		setAdminData(data.id);
		setShowAdminModal(true);
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

	const adminModalCloseHandler = () => {
		setShowAdminModal(false);
	};

	const searchButtonClickHandler = () => {
		setCurrentPageNumber(1);
		dispatch(getCompany(undefined, searchString));
	};

	useEffect(() => {
		if (status === idle) {
			dispatch(getCompany());
		}
	}, [dispatch, status]);

	return (
		<>
			{status === succeeded ? (
				<>
					<section className="site-breadcrumb bg-light">
						<Container className="text-center">
							<Row>
								<Col>
									<h5 className="mb-0">Manage Company</h5>
									<Breadcrumb className="justify-content-center">
										<Breadcrumb.Item>Home</Breadcrumb.Item>
										<Breadcrumb.Item active>Company</Breadcrumb.Item>
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
													onChange={(e) => setSearchString(e.target.value)}
													value={searchString}
												/>
												<InputGroup.Append>
													<Button
														variant="primary"
														onClick={() => searchButtonClickHandler()}
													>
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
												<FontAwesomeIcon icon={faPlusCircle} /> Add Company
											</Button>
										</header>
										{showAddModal ? (
											<AddCompany
												show={showAddModal}
												onClose={addModalCloseHandler}
												pageNumber={currentPageNumber}
											/>
										) : (
											""
										)}
										{showEditModal ? (
											<EditCompany
												show={showEditModal}
												onClose={editModalCloseHandler}
												data={editData}
											/>
										) : (
											""
										)}
										{showDeleteModal ? (
											<DeleteCompany
												show={showDeleteModal}
												onClose={deleteModalCloseHandler}
												data={deleteData}
											/>
										) : (
											""
										)}
										{showAdminModal ? (
											<ManageAdmin
												show={showAdminModal}
												onClose={adminModalCloseHandler}
												data={adminData}
											/>
										) : (
											""
										)}
										<Table striped size="">
											<thead className="bg-light text-dark">
												<tr>
													<th>Name</th>
													<th>Registration No</th>
													<th>Email</th>
													<th>IsActive</th>
													<th></th>
												</tr>
											</thead>
											<tbody>
												{companyInfo.data.length > 0 ? (
													companyInfo.data.map((company) => {
														return (
															<tr key={company.id}>
																<td>{company.name}</td>
																<td>{company.registrationNo}</td>
																<td>{company.email}</td>
																<td>
																	{company.isActive ? (
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
																					editHandler(company);
																				}}
																			>
																				<FontAwesomeIcon
																					icon={faPencilAlt}
																					className="mr-2"
																				/>{" "}
																				Edit
																			</Dropdown.Item>
																			{company.isActive ? (
																				<Dropdown.Item
																					onClick={() => {
																						deleteHandler(company);
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
																				// onClick={() => {
																				// 	restoreHandler(company);
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
															totalRecords={companyInfo.count}
															callback={getCompany}
															searchQuery={searchString}
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

export default Company;
