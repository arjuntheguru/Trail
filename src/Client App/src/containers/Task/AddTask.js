import React from "react";
import { Modal } from "react-bootstrap";
import TaskForm from "./TaskForm";

const AddTask = ({ show, onClose, pageNumber }) => {
	return (
		<Modal show={show} onHide={onClose} centered size="lg">
			<Modal.Body className="bgGradient">
				{<TaskForm onClose={onClose} pageNumber={pageNumber} />}
			</Modal.Body>
		</Modal>
	);
};

export default AddTask;
