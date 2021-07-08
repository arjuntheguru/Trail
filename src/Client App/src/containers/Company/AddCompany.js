import React from "react";
import { Modal } from "react-bootstrap";
import CompanyForm from "./CompanyForm";

const AddCompany = ({ show, onClose, pageNumber }) => {
	return (
		<Modal show={show} onHide={onClose} centered size="lg">
			<Modal.Body className="bgGradient">
				{<CompanyForm onClose={onClose} pageNumber={pageNumber} />}
			</Modal.Body>
		</Modal>
	);
};

export default AddCompany;
