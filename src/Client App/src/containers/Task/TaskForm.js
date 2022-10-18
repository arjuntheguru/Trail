import React, { useState } from "react";
import { Formik, Form, FieldArray, Field } from "formik";
import { useDispatch } from "react-redux";
import {
	addTask,
	getTask,
	updateTask,
	getTaskAdmin,
} from "../../store/task-actions";
import { InputField } from "../../components/InputField/InputField";
import { TextArea } from "../../components/TextArea/TextArea";
import * as Yup from "yup";

import {
	Card,
	Button,
	Row,
	Col,
	Popover,
	OverlayTrigger,
	Modal,
} from "react-bootstrap";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlusCircle, faTrash } from "@fortawesome/free-solid-svg-icons";

import { returnLoggedInUser } from "../../store/auth-actions";

const TaskForm = ({ isEdit = false, data = {}, onClose, pageNumber }) => {
	const initialValues = {
		id: "",
		name: "",
		companyId: null,
		description: "",
		taskType: "checkList",
		taskItemInfo: [{ name: "" }],
	};

	const initialValuesEdited = {
		id: data.id,
		name: data.name,
		description: data.description,
		companyId: data.companyId,
		taskItemInfo: data.taskItemInfo,
	};

	const [showPopover, setShowPopover] = useState(false);
	const [isCheckList, setisCheckList] = useState(true);

	const radioValueChangeHandler = (e) => {
		console.log(e);
		if (e.target.defaultValue === "checkList") {
			setisCheckList(true);
		} else {
			setisCheckList(false);
		}
	};

	const popover = (
		<Popover id="popover-basic">
			<Popover.Content>
				Atleast <strong>one</strong> item is required.
			</Popover.Content>
		</Popover>
	);

	const taskInfoValidate = Yup.object().shape({
		name: Yup.string()
			.max(50, "Must be 15 characters or less")
			.required("Name is required"),
		description: Yup.string().required("Description is required"),
		taskItemInfo: Yup.array().of(
			Yup.object().shape({
				name: Yup.string().required("Task Item is required"),
			})
		),
	});

	const dispatch = useDispatch();

	const user = dispatch(returnLoggedInUser());

	const addTaskItemHandler = (values, setValues, setTouched) => {
		const taskItemInfo = [...values.taskItemInfo];
		setTouched({});
		taskItemInfo.push({ name: "" });
		setValues({ ...values, taskItemInfo });
		//setShowPopover(false)
	};

	const deleteTaskHandler = (e, values, setValues, setTouched, name) => {
		const taskItemInfo = [...values.taskItemInfo];
		const index = parseInt(e.currentTarget.dataset.index);

		if (taskItemInfo.length > 1) {
			taskItemInfo.splice(index, 1);
			setValues({ ...values, taskItemInfo });
			setShowPopover(false);
			setTouched({});
		} else {
			setShowPopover(true);
		}
	};

	return (
		<Formik
			initialValues={!isEdit ? initialValues : initialValuesEdited}
			validationSchema={taskInfoValidate}
			onSubmit={(values) => {
				let taskInfo = {
					Id: values.id,
					Name: values.name,
					Description: values.description,
					CompanyId: user.role === "Admin" ? user.companyId : null,
					TaskItemInfo: values.taskItemInfo,
				};

				if (!isEdit) {
					const callback = () => {
						user.role === "SuperAdmin"
							? dispatch(getTask({ pageNumber: pageNumber, pageSize: 10 }))
							: dispatch(
									getTaskAdmin({ pageNumber: pageNumber, pageSize: 10 })
							  );
					};
					console.log(values);
					dispatch(addTask(taskInfo, callback));
					onClose();
				} else {
					console.log(values);
					dispatch(updateTask(taskInfo));
					onClose();
				}
			}}
		>
			{({ values, setValues, setTouched }) => (
				<div>
					<Modal.Header closeButton>
						{!isEdit ? "Add Task" : "Edit Task"}
					</Modal.Header>
					<Form>
						<Modal.Body>
							<InputField label="Name" name="name" type="text" />
							<TextArea label="Description" name="description" />
							{/* <div role="group" onChange={e => radioValueChangeHandler(e)}>
                                    <label className="mr-3">Task Type : </label>
                                    <label className="mr-3">
                                        <Field type="radio" name="taskType" value="checkList" /> Checklist
                                    </label>
                                    <label className="mr-3">
                                        <Field type="radio" name="taskType" value="recordLog" /> Record Log
                                    </label>
                                </div> */}
							{isCheckList && (
								<>
									<h6>Task Item </h6>
									<div className="list-content-wrapper">
										<FieldArray name="taskItemInfo">
											{() =>
												values.taskItemInfo.map((taskItem, i) => {
													return (
														<div key={i} className="d-flex justify-content-start mb-2">
															{/* <span className="">{i + 1}.</span> */}

															<InputField
																isLabel={false}
																name={`taskItemInfo.${i}.name`}
																type="text"
															/>
															<OverlayTrigger
																overlay={popover}
																show={showPopover}
																trigger="click"
																onToggle={() => {
																	setShowPopover(!showPopover);
																}}
																rootClose
															>
																<Button
																	variant="link"
																	size="sm"
																	data-index={i}
																	onClick={(e) =>
																		deleteTaskHandler(
																			e,
																			values,
																			setValues,
																			setTouched
																		)
																	}
																	type="button"
																>
																	<FontAwesomeIcon icon={faTrash} />
																</Button>
															</OverlayTrigger>
														</div>
													);
												})
											}
										</FieldArray>
									</div>
									<Button
										type="button"
										variant="link"
										className="my-2"
										onClick={() => {
											addTaskItemHandler(values, setValues, setTouched);
										}}
									>
										<FontAwesomeIcon icon={faPlusCircle} /> Add Item
									</Button>
								</>
							)}
						</Modal.Body>
						<Modal.Footer className="justify-content-between">
							<Button variant="light" type="reset">
								Reset
							</Button>
							<Button variant="primary" type="submit">
								Submit
							</Button>
						</Modal.Footer>
					</Form>
				</div>
			)}
		</Formik>
	);
};

export default TaskForm;
