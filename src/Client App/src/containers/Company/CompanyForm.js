import React from "react";
import { Formik, Form } from "formik";
import { useDispatch } from "react-redux";
import {
	addCompany,
	getCompany,
	updateCompany,
} from "../../store/company-actions";
import { InputField } from "../../components/InputField/InputField";
import * as Yup from "yup";

import { Card, Button } from "react-bootstrap";

const CompanyForm = ({ isEdit = false, data = {}, onClose, pageNumber }) => {
	const companyInfoValidate = Yup.object({
		name: Yup.string()
			.max(15, "Must be 15 characters or less")
			.required("Name is required"),
		registrationNo: Yup.string()
			.max(20, "Must be 20 characters or less")
			.required("Registration No is required"),
		email: Yup.string().email("Email is invalid").required("Email is required"),
		streetAddress: Yup.string().required("Street Address is required"),
		city: Yup.string().required("City is required"),
		country: Yup.string().required("Country is required"),
	});

	const userInfoValidate = Yup.object({
		firstName: Yup.string().required("First Name is required"),
		lastName: Yup.string().required("Last Name is required"),
		userEmail: Yup.string()
			.email("Email is invalid")
			.required("User Email is required"),
		userName: Yup.string().required("User Name is required"),
		password: Yup.string()
			.min(6, "Must be 6 characters or more")
			.required("Password is required"),
		confirmPassword: Yup.string()
			.required("Confirm Password is required")
			.oneOf([Yup.ref("password")], "Passwords must match"),
	});

	const dispatch = useDispatch();

	return (
		<Formik
			initialValues={
				!isEdit
					? {
							id: "",
							name: "",
							registrationNo: "",
							email: "",
							streetAddress: "",
							addressLine2: "",
							city: "",
							state: "",
							country: "",
							zip: "",
							firstName: "",
							lastName: "",
							userName: "",
							userEmail: "",
							password: "",
							confirmPassword: "",
					  }
					: {
							id: data.id,
							name: data.name,
							registrationNo: data.registrationNo,
							email: data.email,
							streetAddress: data.address.streetAddress,
							addressLine2: data.address.addressLine2,
							city: data.address.city ? data.address.city : "",
							state: data.address.state,
							country: data.address.country,
							zip: data.address.zip,
					  }
			}
			validationSchema={
				!isEdit
					? companyInfoValidate.concat(userInfoValidate)
					: companyInfoValidate
			}
			onSubmit={(values) => {
				let companyInfo = {
					Id: values.id,
					Name: values.name,
					RegistrationNo: values.registrationNo,
					Email: values.email,
					Address: {
						StreetAddress: values.streetAddress,
						AddressLine2: values.addressLine2,
						State: values.state,
						City: values.city,
						Country: values.country,
						ZIP: values.zip,
					},
				};

				if (!isEdit) {
					console.log("Values");
					const userInfo = {
						firstName: values.firstName,
						lastName: values.lastName,
						userName: values.userName,
						email: values.userEmail,
						password: values.password,
						role: "Admin",
					};

					const callback = () => {
						dispatch(getCompany({ pageNumber: pageNumber, pageSize: 10 }));
					};

					dispatch(addCompany(companyInfo, userInfo, callback));
					onClose();
				} else {
					console.log(values);
					dispatch(updateCompany(companyInfo));
					onClose();
				}
			}}
		>
			{(formik) => (
				<div>
					<Card className="bg-transparent">
						<Card.Header className="bg-dark text-light text-center  ">
							{!isEdit ? "Add Company" : "Edit Company"}
						</Card.Header>
						<Form>
							<Card.Body className="card-body">
								<div className="form-row">
									<InputField
										label="Name"
										name="name"
										type="text"
										size={50}
										readOnly={!isEdit ? false : true}
									/>
									<InputField
										label="Registration No."
										name="registrationNo"
										type="text"
										size={50}
										readOnly={!isEdit ? false : true}
									/>
								</div>
								<InputField label="Email" name="email" type="text" />
								<Card className="mt-1 bg-transparent">
									<Card.Header className="bg-dark text-light text-center">
										Address
									</Card.Header>
									<Card.Body>
										<div className="form-row">
											<InputField
												label="Street Address"
												name="streetAddress"
												type="text"
												size={50}
											/>
											<InputField
												label="Address Line 2"
												name="addressLine2"
												type="text"
												size={50}
											/>
										</div>
										<div className="form-row">
											<InputField
												label="City"
												name="city"
												type="text"
												size={50}
											/>
											<InputField
												label="State"
												name="state"
												type="text"
												size={50}
											/>
										</div>
										<div className="form-row">
											<InputField
												label="Country"
												name="country"
												type="text"
												size={50}
											/>
											<InputField
												label="ZIP"
												name="zip"
												type="text"
												size={50}
											/>
										</div>
									</Card.Body>
								</Card>
								{!isEdit ? (
									<Card className="mt-1 bg-transparent">
										<Card.Header className="bg-dark text-light text-center">
											Admin Information
										</Card.Header>
										<Card.Body>
											<div className="form-row">
												<InputField
													label="First Name"
													name="firstName"
													type="text"
													size={50}
												/>
												<InputField
													label="Last Name"
													name="lastName"
													type="text"
													size={50}
												/>
											</div>
											<div className="form-row">
												<InputField
													label="Username"
													name="userName"
													type="text"
													size={50}
												/>
												<InputField
													label="Email"
													name="userEmail"
													type="text"
													size={50}
												/>
											</div>
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
										</Card.Body>
									</Card>
								) : (
									""
								)}
							</Card.Body>
							<Card.Footer className="text-right">
								<Button variant="success" type="submit" size="sm">
									Submit
								</Button>
								<Button
									variant="danger"
									className="ml-3"
									type="reset"
									size="sm"
								>
									Reset
								</Button>
							</Card.Footer>
						</Form>
					</Card>
				</div>
			)}
		</Formik>
	);
};

export default CompanyForm;
