import React from "react";
import { Modal } from "react-bootstrap";
import TaskForm from "./TaskForm";

const AddTask = ({ show, onClose, pageNumber }) => {
	return (
		<Modal show={show} onHide={onClose} centered>
				{<TaskForm onClose={onClose} pageNumber={pageNumber} />}
			
		</Modal>
	);
};

export default AddTask;
