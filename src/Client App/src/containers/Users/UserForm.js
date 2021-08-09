import React, { useState } from "react";
import { Formik, Form, Field } from "formik";
import { useDispatch } from "react-redux";
import {  } from "react-bootstrap";
import { InputField } from "../../components/InputField/InputField";
import * as Yup from "yup";
import { Button, Row, Col, Modal, Tabs, Tab, Sonnet } from "react-bootstrap";
import { registerUser, getUser, updateUser } from "../../store/user-actions";

import _ from "lodash";
import { returnLoggedInUser } from "../../store/auth-actions";

import ChangeUserPassword from "./ChangeUserPassword";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSave, faTimes, faCheckCircle, faUserAlt, faUserTie, faUserCog  } from "@fortawesome/free-solid-svg-icons";

const UserForm = ({ isEdit = false, siteInfo, data, onClose, pageNumber }) => {
	const [showChangePasswordModal, setShowChangePasswordModal] = useState(false);
	const [userName, setUserName] = useState("");

	const changePasswordHandler = (params) => {
		setUserName(params);
		setShowChangePasswordModal(true);
	};

	const changePasswordModalCloseHandler = () => {
		setShowChangePasswordModal(false);
	};

	const dispatch = useDispatch();

	const user = dispatch(returnLoggedInUser());

	const userInfoValidate = Yup.object({
		firstName: Yup.string().required("First Name is required"),
		lastName: Yup.string().required("Last Name is required"),
		userEmail: Yup.string()
			.email("Email is invalid")
			.required("User Email is required"),
		userName: Yup.string().required("User Name is required"),
		role: Yup.string().required("Role is required"),
	});

	const passwordValidate = Yup.object({
		password: Yup.string()
			.min(6, "Must be 6 characters or more")
			.required("Password is required"),
		confirmPassword: Yup.string()
			.required("Confirm Password is required")
			.oneOf([Yup.ref("password")], "Passwords must match"),
	});

	return (
		<>
			{showChangePasswordModal ? (
				<ChangeUserPassword
					show={showChangePasswordModal}
					onClose={changePasswordModalCloseHandler}
					data={userName}
				/>
			) : (
				""
			)}
			<Formik
				initialValues={
					!isEdit
						? {
								id: "",
								firstName: "",
								lastName: "",
								userName: "",
								userEmail: "",
								password: "",
								confirmPassword: "",
								role: "Team User",
								site: siteInfo.data[0].id,
						  }
						: {
								id: data.id,
								firstName: data.firstName,
								lastName: data.lastName,
								userName: data.userName,
								userEmail: data.email,
								companyId: data.companyId,
								role: data.role,
								site: data.siteId,
						  }
				}
				validationSchema={
					!isEdit ? userInfoValidate.concat(passwordValidate) : userInfoValidate
				}
				onSubmit={(values) => {
					const userInfo = {
						Id: values.id,
						FirstName: values.firstName,
						LastName: values.lastName,
						UserName: values.userName,
						Email: values.userEmail,
						Password: values.password,
						Role: values.role,
						SiteId:
							values.role === "Admin"
								? null
								: _.isArray(values.site)
								? values.site
								: [values.site],
					};

					console.log(values);

					if (!isEdit) {
						const callback = () => {
							dispatch(getUser({ pageNumber: pageNumber, pageSize: 10 }));
						};

						dispatch(registerUser(userInfo, callback));
						onClose();
					} else {
						dispatch(updateUser(userInfo));
						onClose();
					}
				}}
			>
				{({ values }) => (
					<>
						<Modal.Header closeButton>
							{!isEdit ? "Add User" : "Edit User"}
						</Modal.Header>
						<Form>
							<Modal.Body>
									<InputField
										label="First Name"
										name="firstName"
										type="text"
									/>
									<InputField
										label="Last Name"
										name="lastName"
										type="text"
									/>
								
								<Tabs defaultActiveKey="profile" id="uncontrolled-tab-example" className="mb-3 clean-tabs">
									<Tab eventKey="login" title="Login Details">
											<InputField
												label="Username"
												name="userName"
												type="text"
											/>
											<InputField
												label="Email"
												name="userEmail"
												type="text"
											/>
											<div className="form-row">
											<InputField
													label="Password"
													name="password"
													type="password"
													size={50}
												/>
											<InputField
												label="Confirm Password"
												name="confirmPassword"
												type="password"
												size={50}
											/>
											</div>
											
											<h6>Password must contain</h6>
											<ul className="fa-ul">
												<li><span className="fa-li"><FontAwesomeIcon icon={faCheckCircle} /> </span> 8 or more characters</li>
												<li><span className="fa-li"><FontAwesomeIcon icon={faCheckCircle} /> </span> Upper &amp; lowercase letters</li>
												<li><span className="fa-li"><FontAwesomeIcon icon={faCheckCircle} /> </span> At least one number</li>
											</ul>
										{isEdit && (
												<Button
													block
													variant="outline-dark"
													onClick={() => changePasswordHandler(data.userName)}
												>
													Change Password
												</Button>
											)}
										{!isEdit && (
											<>
												<InputField
													label="Password"
													name="password"
													type="password"
													
												/>
												<InputField
													label="Confirm Password"
													name="confirmPassword"
													type="password"
													
												/>
											</>
										)}
									</Tab>
									<Tab eventKey="role" title="Manage Roles">
										<h6>Choose Role</h6>
										<div className="radio-block d-flex" role="group" aria-label="Basic checkbox toggle button group">
											<label>
												<Field type="radio" name="role" value="teamuser" />
												<div className="box d-flex justify-content-center align-items-center">
												<FontAwesomeIcon icon={faUserAlt} className="mr-2"/> Team User
												</div>
											</label>
										<label>
											<Field type="radio" name="role" value="manager" />
											<div className="box d-flex justify-content-center align-items-center">
											<FontAwesomeIcon icon={faUserTie} className="mr-2"/> Manager
											</div>
											
										</label>
										<label>
										<Field type="radio" name="role" value="admin" />
										<div className="box d-flex justify-content-center align-items-center">
										<FontAwesomeIcon icon={faUserCog} className="mr-2"/> Admin
										</div>
										
										</label>
										</div>
										<h6>Choose Site</h6>
										<Row>
										<Col lg="4">
											<label>
											<Field type="checkbox" name="checked" value="All" />
											All
											</label>
											</Col>
											<Col lg="4">
											<label>
											<Field type="checkbox" name="checked" value="site1" />
											Site 1
											</label>
											</Col>
											<Col lg="4">
											<label>
											<Field type="checkbox" name="checked" value="site2" />
											Site 2
											</label>
											</Col>
											<Col lg="4">
											<label>
											<Field type="checkbox" name="checked" value="site3" />
											Site 3
											</label>
											</Col>
											<Col lg="4">
											<label>
											<Field type="checkbox" name="checked" value="site4" />
											Site 4
											</label>
											</Col>
											<Col lg="4">
											<label>
											<Field type="checkbox" name="checked" value="site5" />
											Site 5
											</label>
											</Col>
											<Col lg="4">
											<label>
											<Field type="checkbox" name="checked" value="site6" />
											Site 6
											</label>
											</Col>
										</Row>
											{!_.isEmpty(siteInfo) && (
												<>
													<Col md={6}>
														<div className="form-group">
															<label htmlFor="role">Role</label>
															<Field
																as="select"
																className="form-control shadow-none"
																name="role"
																disabled={
																	user.userName === data.userName ? true : false
																}
															>
																<option value="Team User">Team User</option>
																<option value="Manager">Manager</option>
																<option value="Admin">Admin</option>
															</Field>
														</div>
													</Col>
													<Col md={6}>
														<div className="form-group">
															<label htmlFor="site">Sites</label>
															{values.role !== "Admin" ? (
																<Field
																	as="select"
																	className="form-control shadow-none"
																	name="site"
																	multiple={
																		values.role === "Manager" ? true : false
																	}
																>
																	{siteInfo.data.map((site) => {
																		return (
																			<option value={site.id}>{site.name}</option>
																		);
																	})}
																</Field>
															) : (
																<p className="text-muted form-control form-control">
																	Admin has acess to all sites
																</p>
															)}
														</div>
													</Col>{" "}
												</>
											)}
											
										
									</Tab>
									
								</Tabs>
								
								
							</Modal.Body>
							<Modal.Footer className="justify-content-between">
								<Button
									variant="light"
									type="button"
									className="m-0"
									onClick={() => {
										onClose(true);
									}}
								>
									<FontAwesomeIcon icon={faTimes} /> Cancel
								</Button>
								<Button variant="primary" type="submit" className="m-0">
									<FontAwesomeIcon icon={faSave} /> Save
								</Button>
							</Modal.Footer>
						</Form>
					</>
				)}
			</Formik>
		</>
	);
};

export default UserForm;
